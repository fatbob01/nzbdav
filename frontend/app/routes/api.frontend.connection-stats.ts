import type { Route } from "./+types/api.frontend.connection-stats";

export async function loader({ request }: Route.LoaderArgs) {
    const { backendClient } = await import("~/clients/backend-client.server");
    try {
        const connectionStats = await backendClient.getConnectionStats();
        return Response.json(connectionStats);
    } catch (error) {
        console.error('Connection stats error:', error);
        return Response.json(
            { error: error instanceof Error ? error.message : 'Failed to fetch connection stats' },
            { status: 500 }
        );
    }
}