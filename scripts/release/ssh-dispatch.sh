#!/usr/bin/env bash
# Entry point for a dedicated authorized_keys forced command. No shell, SCP or forwarding.
set -euo pipefail
command=${SSH_ORIGINAL_COMMAND:-}
if [[ "$command" =~ ^deploy\ ([0-9a-f]{40})$ ]]; then
    exec sudo -n /usr/local/sbin/restaurant-deploy "${BASH_REMATCH[1]}"
fi
printf 'Only deploy followed by a full commit SHA is accepted.\n' >&2
exit 2
