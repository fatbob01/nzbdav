# About `/content`

This directory contains all streamable files that have finished processing from the nzbdav queue.

It is mostly read-only. Files cannot be created, renamed, or moved within this directory.

However, files can be deleted from this directory if you no longer want them, using any compatible webdav client (or using Rclone). But only when the setting under `Settings -> WebDAV -> Enforce Read-Only` is disabled. Deleting items from this directory may break symlinks that point to those items, so be certain you no longer need them before doing so.

> Note: You can run the maintenance task under `Settings -> Maintenance -> Remove Orphaned Files` to remove all files from the `/content` folder that are no longer symlinked by your media library.

---

# About `/symlinks`

When the Symlink Mirror Directory setting is configured, this directory exposes real filesystem symlinks created by nzbdav. These symlinks point to items under `/content`, so Arrs can import from `/symlinks/...` and land on the real streamable files.

This folder mirrors the local symlink mirror directory you configured (for example `/symlinks` inside the container).

If your rclone mount directory is on another machine, set `symlink.mirror-target-dir` (or `SYMLINK_MIRROR_TARGET_DIR`) to a local mount path on the nzbdav host so the symlink targets resolve.

> Legacy note: if you are not using the mirror, the older `/completed-symlinks` tree uses `*.rclonelink` files and requires `rclone --links` to translate them into symlinks.

---

# About `/nzbs`

This directory mirrors the nzbdav queue
* Any nzb currently in the queue can be retrieved from this directory.
* You can remove items from the queue by deleting the corresponding nzb from this directory
* You can add items to the queue by uploading nzb files to this directory

> Note: You must perform file operations using any compatible webdav client (or Rclone). The "Dav Explore" page on nzbdav UI does not currently support file operations.
