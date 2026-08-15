# GitHub Release Process

`release/release.json` is the release metadata source of truth for the GitHub channel.

A release build must:

1. validate product, package, project, and manifest versions;
2. restore and run core tests;
3. build an unsigned x64 MSIXBundle;
4. load the release PFX from GitHub Actions secrets;
5. sign the bundle with SHA-256 and a timestamp;
6. export the public release certificate;
7. generate and validate the one-click bootstrap package;
8. generate `SHA256SUMS.txt`;
9. publish three assets only: signed MSIXBundle, one-click ZIP, and SHA256 manifest;
10. publish tag-pinned Simplified Chinese, Japanese, and English Release Notes links.

Required GitHub Actions secrets:

- `GH_RELEASE_CERTIFICATE_BASE64`
- `GH_RELEASE_CERTIFICATE_PASSWORD`

Never commit the PFX, private key, password, or decoded certificate secret to the repository.
