import { Alert, Button, Form as BootstrapForm } from "react-bootstrap";
import styles from "./route.module.css"
import type { Route } from "./+types/route";
import { Form, redirect, useNavigation } from "react-router";

type LoginPageData = {
    loginError: string
}

export async function loader({ request }: Route.LoaderArgs) {
    const { sessionStorage } = await import("~/auth/authentication.server");
    const { backendClient } = await import("~/clients/backend-client.server");
    // if already logged in, redirect to landing page
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (user) return redirect("/");

    // if we need to go through onboarding, redirect to onboarding page
    const isOnboarding = await backendClient.isOnboarding();
    if (isOnboarding) return redirect("/onboarding");

    // otherwise, proceed to login page!
    return { loginError: null };
}

export default function Index({ loaderData, actionData }: Route.ComponentProps) {
    const navigation = useNavigation();
    const isLoading = navigation.state == "submitting";
    const pageData = actionData || loaderData;
    const showError = !!pageData.loginError;
    const submitButtonDisabled = isLoading;
    const submitButtonText = isLoading ? "Logging in..." : "Login";

    return (
        <>
            <Form className={styles["container"]} method="POST">
                <img className={styles["logo"]} src="/logo.svg"></img>
                <div className={styles["title"]}>Nzb DAV</div>
                <Alert className={styles["error"]} show={showError} variant="danger">{pageData.loginError}</Alert>
                <BootstrapForm.Control name="username" type="text" placeholder="Username" autoFocus />
                <BootstrapForm.Control name="password" type="password" placeholder="Password" />
                <Button type="submit" variant="primary" disabled={submitButtonDisabled}>{submitButtonText}</Button>
            </Form>
        </>
    );
}

export async function action({ request }: Route.ActionArgs) {
    try {
        const { authenticate, sessionStorage } = await import("~/auth/authentication.server");
        const user = await authenticate(request);
        const session = await sessionStorage.getSession(request.headers.get("cookie"));
        session.set("user", user);
        return redirect("/", { headers: { "Set-Cookie": await sessionStorage.commitSession(session) } });
    }
    catch (error) {
        if (error instanceof Error) {
            return { loginError: error.message };
        }
        throw error;
    }
}