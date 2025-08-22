import type { Route } from "./+types/route";
import type { ConfigItem } from "~/clients/backend-client.server";
import { redirect } from "react-router";

export async function action({ request }: Route.ActionArgs) {
    const { sessionStorage } = await import("~/auth/authentication.server");
    const { backendClient } = await import("~/clients/backend-client.server");
    // ensure user is logged in
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    // get the ConfigItems to update
    const formData = await request.formData();
    const configJson = formData.get("config")!.toString();
    const config = JSON.parse(configJson) as Record<string, unknown>;
    const configItems: ConfigItem[] = [];
    for (const [key, value] of Object.entries(config)) {
        configItems.push({
            configName: key,
            configValue: String(value)
        });
    }

    // update the config items
    await backendClient.updateConfig(configItems);
    return { config: config }
}