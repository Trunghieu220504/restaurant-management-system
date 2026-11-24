#!/usr/bin/env bash
set -euo pipefail
exec java -jar /opt/jenkins/agent.jar -url "${JENKINS_URL:?}" \
    -secret "${JENKINS_SECRET:?}" -name "${JENKINS_AGENT_NAME:?}" \
    -webSocket -workDir /home/jenkins/agent
