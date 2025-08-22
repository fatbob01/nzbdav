import { Button } from "react-bootstrap";
import styles from "./maintenance.module.css";
import React from "react";
import { useFetcher } from "react-router";

export function MaintenanceSettings() {
    const [messages, setMessages] = React.useState<string[]>([]);
    const fetcher = useFetcher();

    React.useEffect(() => {
        const url = (process.env.BACKEND_URL || "").replace(/^http/, "ws") + "/ws";
        const ws = new WebSocket(url);
        ws.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data);
                if (data.topic === "MigrateLibrarySymlinksProgress") {
                    setMessages(prev => [...prev, JSON.stringify(data.payload)]);
                }
            } catch { }
        };
        return () => ws.close();
    }, []);

    React.useEffect(() => {
        if (fetcher.state === "idle" && fetcher.data) {
            if (fetcher.data.error) {
                setMessages(prev => [...prev, `Error: ${fetcher.data.error}`]);
            } else {
                setMessages(prev => [...prev, "Migration started"]);
            }
        }
    }, [fetcher.state, fetcher.data]);

    const onMigrate = () => {
        fetcher.submit(null, { method: "post", action: "/api/migrate-library-symlinks" });
    };

    return (
        <div className={styles.container}>
            <Button onClick={onMigrate}>Migrate Library Symlinks</Button>
            <ul>
                {messages.map((m, i) => <li key={i}>{m}</li>)}
            </ul>
        </div>
    );
}
