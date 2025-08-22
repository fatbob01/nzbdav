import type { Route } from "./+types/route";
import { redirect } from "react-router";

export async function action({ request }: Route.ActionArgs) {
    const { sessionStorage } = await import("~/auth/authentication.server");
    // if already logged out, redirect to login page
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    // if we logout intent is not confirmed, redirect to landing page
    const formData = await request.formData();
    const confirm = formData.get("confirm")?.toString();
    if (confirm !== "true") return redirect("/");

    // otherwise, proceed to log out!
    session.unset("user");
    return redirect("/login", { headers: { "Set-Cookie": await sessionStorage.commitSession(session) } });
}