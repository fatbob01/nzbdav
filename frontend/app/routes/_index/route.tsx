import { redirect } from "react-router";
import type { Route } from "./+types/route";
import { sessionStorage } from "~/auth/authentication.server";

export async function loader({ request }: Route.LoaderArgs) {
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");
    return redirect("/queue")
}