import type { Route } from "./+types/api.migrate-library-symlinks";
import { backendClient } from "~/clients/backend-client.server";

export async function action({ request }: Route.ActionArgs) {
    try {
        await backendClient.migrateLibrarySymlinks();
        return Response.json({ status: "ok" });
    } catch (error) {
        console.error("Migrate library symlinks error:", error);
        return Response.json(
            { error: error instanceof Error ? error.message : "Failed to migrate library symlinks" },
            { status: 500 }
        );
    }
}
