import { Button } from "react-bootstrap";
import styles from "./maintenance.module.css";
import React from "react";

export function MaintenanceSettings() {
    const [messages, setMessages] = React.useState<string[]>([]);

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

    const onMigrate = async () => {
        await fetch("/api.backend.migrate-library-symlinks", { method: "POST" });
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
