import type { Route } from "./+types/api.backend.migrate-library-symlinks";
import { redirect } from "react-router";

export async function action({ request }: Route.ActionArgs) {
    const { sessionStorage } = await import("~/auth/authentication.server");
    const { backendClient } = await import("~/clients/backend-client.server");
    const session = await sessionStorage.getSession(request.headers.get("cookie"));
    const user = session.get("user");
    if (!user) return redirect("/login");
    await backendClient.migrateLibrarySymlinks();
    return new Response(null, { status: 204 });
}
