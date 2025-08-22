import { Form } from "react-bootstrap";
import styles from "./library.module.css";
import { type Dispatch, type SetStateAction } from "react";

export type LibrarySettingsProps = {
    config: Record<string, string>;
    setNewConfig: Dispatch<SetStateAction<Record<string, string>>>;
};

export function LibrarySettings({ config, setNewConfig }: LibrarySettingsProps) {
    return (
        <div className={styles.container}>
            <Form.Group>
                <Form.Label htmlFor="library-dir-input">Library Directory</Form.Label>
                <Form.Control
                    className={styles.input}
                    type="text"
                    id="library-dir-input"
                    aria-describedby="library-dir-help"
                    value={config["media.library-dir"]}
                    onChange={e => setNewConfig({ ...config, "media.library-dir": e.target.value })}
                />
                <Form.Text id="library-dir-help" muted>
                    Path to the directory where your library resides.
                </Form.Text>
            </Form.Group>
        </div>
    );
}
