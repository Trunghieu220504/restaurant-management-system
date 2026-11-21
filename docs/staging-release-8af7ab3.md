# Staging release 8af7ab3

Runtime source: merged develop `8af7ab35612a95cb5f2a07778fccf0b3c8f31b6e`, including the merged responsive layout #81, UI cleanup #76 and Identity account API #77. No PR was merged by the deployment operator.

## Verified inputs

- Backend/SQL/image publication: [run 44483113835](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44483113835), all jobs successful.
- Web: [run 44483113696](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44483113696), successful.
- Security: [run 44483113952](https://github.com/Trunghieu220504/restaurant-management-system/actions/runs/44483113952), successful.
- API: `ghcr.io/trunghieu220504/restaurant-api@sha256:4d24da5439a09bcdc14072bad2ac367927c15660213470e7394e950021c64b1f`.
- Web: `ghcr.io/trunghieu220504/restaurant-web@sha256:8e0bad15ab9035bb08a542c56a957813ddc8f2ee2fdfe8a2bc56da7c2d9952d0`.
- Public Linux amd64 image configuration labels independently verified from the VPS: both revisions exactly match the source above.
- Deploy helper SHA-256: `864a963e7e1f63b0d6c84a31e84c1223ee468e5bb48aa43126688d66f32239d8`.
- Installer SHA-256: `935f32a9e4f972a194134b30142783d2b1234639e36756656fd824d4e5ced556`.

The deploy helper is the merged #73 helper with only revision, release path/name and image digest substitutions. The UTF-8/BOM SQL transport and backup/restore/rollback checks are retained. There are no migration or infra changes between the earlier runtime 95ab29b and this source; original Seating/Sales SQL checksums are unchanged. Inputs uploaded under `/home/deploy/restaurant-release-8af7ab3` passed remote checksum and Bash syntax checks; installer rejects non-root/arguments.

## One-time operator installation

Run inside the existing SSH session (not Windows PowerShell):

```bash
sudo bash /home/deploy/restaurant-release-8af7ab3/install-approved-staging-8af7ab3.sh
```

The installer verifies fixed helper/SQL hashes after copying to a root-private candidate directory. It copies the existing root-owned 95ab29b Compose configuration, runs a read-only plan, and installs only a no-argument sudo wrapper for this fixed release. No account helper, broad Docker/shell grant, environment edit or deployment runs during installation. Do not run the earlier 95ab29b repair first; this release includes that fix.

After a successful installation, the authorized deployment operator can run:

```bash
sudo -n /usr/local/sbin/restaurant-deploy-approved-8af7ab3
```

That command performs backup + CHECKSUM + VERIFYONLY, isolated restore, two migration passes, CHECKDB, isolated API readiness, staging migration, API/Web replacement and public readiness/revision checks. SQL and Nginx container IDs must be unchanged. On failure after image replacement it attempts to restore the previous API/Web images, without schema downgrade. Evidence and previous image IDs are root-private under the printed backup directory.

## Acceptance status

At bundle preparation, staging still reported Healthy on the previous runtime. Root installation, actual deployment, post-deploy account verification and browser acceptance are **pending**; successful CI/input validation does not prove deployment occurred. Existing Manager/Staff accounts must be verified without resetting their passwords or publishing credentials.
