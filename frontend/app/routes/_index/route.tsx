import { redirect } from "react-router";
import type { Route } from "./+types/route";

export async function loader({ request }: Route.LoaderArgs) {
    const { sessionStorage } = await import("~/auth/authentication.server");
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");
    return redirect("/queue")
}