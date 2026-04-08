This directory stores the maintained patch stack for the fork.

The patches are generated against the upstream Sonarr release recorded in
`.fork/upstream.json` and are replayed by the upstream sync workflow.

To refresh the patch stack after intentional fork changes:

```bash
./scripts/fork/bootstrap-patches.sh
```
