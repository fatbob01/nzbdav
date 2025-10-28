import type { Route } from "./+types/route";
import { backendClient, type ConfigItem } from "~/clients/backend-client.server";
import { redirect } from "react-router";
import { sessionStorage } from "~/auth/authentication.server";

export async function action({ request }: Route.ActionArgs) {
    // ensure user is logged in
    let session = await sessionStorage.getSession(request.headers.get("cookie"));
    let user = session.get("user");
    if (!user) return redirect("/login");

    // get the ConfigItems to update
    const formData = await request.formData();
    const configJson = formData.get("config")!.toString();
    const config = JSON.parse(configJson);
    const configItems: ConfigItem[] = [];
    for (const [key, value] of Object.entries<string>(config)) {
        configItems.push({
            configName: key,
            configValue: value
        })
    }

    // update the config items
    await backendClient.updateConfig(configItems);
    return { config: config }
}