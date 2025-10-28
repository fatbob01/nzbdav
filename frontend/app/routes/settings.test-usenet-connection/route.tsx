import type { Route } from "./+types/route";
import { backendClient } from "~/clients/backend-client.server";
import { redirect } from "react-router";
import { sessionStorage } from "~/auth/authentication.server";

export async function action({ request }: Route.ActionArgs) {
    // ensure user is logged in
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    const formData = await request.formData();
    return await backendClient.testUsenetConnection({
        host: formData.get("host")!.toString(),
        port: formData.get("port")!.toString(),
        useSsl: formData.get("use-ssl")!.toString(),
        user: formData.get("user")!.toString(),
        pass: formData.get("pass")!.toString(),
    });
}