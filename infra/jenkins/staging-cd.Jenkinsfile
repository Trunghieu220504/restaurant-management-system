pipeline {
    agent { label 'restaurant-linux' }
    options { disableConcurrentBuilds(); skipDefaultCheckout(true); timeout(time: 90, unit: 'MINUTES'); buildDiscarder(logRotator(numToKeepStr:'15')) }
    parameters { string(name:'RELEASE_REVISION', defaultValue:'', description:'Empty: latest develop. Exact SHA: only a revision explicitly approved on the VPS.') }
    triggers { pollSCM('H/5 * * * *') }
    environment {
        DOTNET_PROCESSOR_COUNT='2'
        MSBUILDDISABLENODEREUSE='1'
        DOTNET_CLI_USE_MSBUILD_SERVER='0'
        NEXT_PUBLIC_IDENTITY_REFRESH_ENABLED='true'
    }
    stages {
        stage('Checkout approved source') {
            steps {
                script {
                    if (params.RELEASE_REVISION && !(params.RELEASE_REVISION ==~ /[0-9a-f]{40}/)) { error('Invalid revision') }
                    checkout([$class:'GitSCM', branches:[[name:params.RELEASE_REVISION ?: '*/develop']],
                      userRemoteConfigs:[[url:'https://github.com/Trunghieu220504/restaurant-management-system.git']]])
                }
                sh 'git rev-parse HEAD > release-revision.txt'
            }
        }
        stage('SQL and backend') { steps { sh 'bash scripts/release/test-sql.sh' } }
        stage('Frontend') { steps { dir('web') { sh 'npm ci --no-audit --no-fund && npm run lint && npm run format:check && npm test && npm run build' } } }
        stage('Release security') {
            steps {
                sh '''
                    set -eu
                    python3 -m unittest discover -s scripts/release -p 'test_*.py'
                    gitleaks git . --log-opts="--all" --redact=100 --no-banner --exit-code=1
                    dotnet package list --project RestaurantManagement.slnx --vulnerable --include-transitive --format json --output-version 1 --no-restore > artifacts/release/nuget.json
                    node scripts/check-dependency-audit.mjs nuget artifacts/release/nuget.json
                    (cd web && npm audit --json > ../artifacts/release/npm.json) || true
                    node scripts/check-dependency-audit.mjs npm artifacts/release/npm.json
                '''
            }
        }
        stage('Build deployment package') {
            steps {
                sh 'bash scripts/release/export-migrations.sh'
                sh 'python3 /opt/restaurant-ci/package_ci.py'
            }
        }
        stage('Upload and deploy staging') {
            steps {
                withCredentials([sshUserPrivateKey(credentialsId:'restaurant-ci-deploy', keyFileVariable:'DEPLOY_KEY', usernameVariable:'DEPLOY_USER')]) {
                    sh '''
                        set -eu
                        test "$DEPLOY_USER" = deploy
                        revision=$(cat release-revision.txt)
                        ssh -T -o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=/opt/restaurant-ci/known_hosts -o ServerAliveInterval=30 -i "$DEPLOY_KEY" deploy@103.69.97.224 "upload $revision" < artifacts/release/ci-package.tar
                        ssh -T -o BatchMode=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=/opt/restaurant-ci/known_hosts -o ServerAliveInterval=30 -i "$DEPLOY_KEY" deploy@103.69.97.224 "deploy $revision"
                    '''
                }
                sh 'python3 scripts/release/smoke.py https://staging.quanlynh.id.vn --output artifacts/release/server-smoke.json'
            }
        }
    }
    post { always { archiveArtifacts artifacts:'release-revision.txt,artifacts/release/*.json,artifacts/release/trx/*.trx,artifacts/release/migrations/SHA256SUMS', allowEmptyArchive:true } }
}
