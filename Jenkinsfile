// Configure as a Multibranch Pipeline; credentials are attached only to develop.
pipeline {
    agent { label 'restaurant-linux' }
    options {
        disableConcurrentBuilds()
        timeout(time: 60, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '10'))
        skipDefaultCheckout(true)
    }
    triggers { pollSCM('H/5 * * * *') }
    environment {
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        NEXT_TELEMETRY_DISABLED = '1'
    }
    stages {
        stage('Checkout') {
            steps {
                checkout scm
                sh 'git rev-parse HEAD > release-revision.txt'
            }
        }
        stage('Backend and real SQL Server') {
            steps { sh 'bash scripts/release/test-sql.sh' }
        }
        stage('Web') {
            steps {
                dir('web') {
                    sh 'npm ci --no-audit --no-fund'
                    sh 'npm run lint && npm test && npm run build'
                }
            }
        }
        stage('Security and tooling') {
            steps {
                sh '''
                    set -eu
                    python3 -m unittest discover -s scripts/release -p 'test_*.py'
                    node --test scripts/check-dependency-audit.test.mjs
                    dotnet package list --project RestaurantManagement.slnx --vulnerable --include-transitive --format json --output-version 1 --no-restore > artifacts/release/nuget.json
                    node scripts/check-dependency-audit.mjs nuget artifacts/release/nuget.json
                    cd web
                    npm audit --json --audit-level=high > ../artifacts/release/npm.json
                    node ../scripts/check-dependency-audit.mjs npm ../artifacts/release/npm.json
                    cd ..
                    gitleaks git . --log-opts="--all" --redact=100 --no-banner --exit-code=1
                '''
            }
        }
        stage('Migration bundle') {
            steps { sh 'bash scripts/release/export-migrations.sh' }
        }
        stage('Publish develop') {
            when { allOf { branch 'develop'; not { changeRequest() } } }
            steps {
                withCredentials([usernamePassword(credentialsId: 'restaurant-ghcr', usernameVariable: 'REGISTRY_USER', passwordVariable: 'REGISTRY_PASSWORD')]) {
                    sh 'bash scripts/release/publish.sh'
                }
            }
        }
        stage('Deploy staging') {
            when { allOf { branch 'develop'; not { changeRequest() } } }
            steps {
                withCredentials([
                    sshUserPrivateKey(credentialsId: 'restaurant-staging-deploy', keyFileVariable: 'DEPLOY_KEY', usernameVariable: 'DEPLOY_USER'),
                    file(credentialsId: 'restaurant-staging-known-hosts', variable: 'DEPLOY_KNOWN_HOSTS')
                ]) {
                    sh '''
                        set -eu
                        test "$DEPLOY_USER" = deploy
                        revision=$(cat release-revision.txt)
                        ssh -T -o BatchMode=yes -o StrictHostKeyChecking=yes \
                            -o UserKnownHostsFile="$DEPLOY_KNOWN_HOSTS" -o ConnectTimeout=15 \
                            -i "$DEPLOY_KEY" "$DEPLOY_USER@103.69.97.224" "deploy $revision"
                    '''
                }
                sh 'python3 scripts/release/smoke.py https://staging.quanlynh.id.vn --output artifacts/release/smoke.json'
            }
        }
    }
    post {
        always {
            archiveArtifacts artifacts: 'artifacts/release/**,release-revision.txt', allowEmptyArchive: true
        }
        failure {
            echo 'Release failed. Inspect archived evidence; do not mark acceptance PASS.'
        }
    }
}
