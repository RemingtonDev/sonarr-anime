# Public certificate trust qualification

Both Docker recipes require signed Ubuntu CA packages at least
`20260816ubuntu0.22.04.1`. Rebuild with `--pull`; the pinned base digest alone
predates the eMudhra fix. Frontend CI uses supported Node 24 with exclusive OS
trust, since the official Node bundled-CA incident release was still pending
when this change was prepared. These changes do not migrate the .NET framework.

Use the Build workflow's `verify_public_trust: true` input for incident
qualification. It checks the frontend client and both published image
architectures before creating a release or updating `latest`. Ordinary builds
keep caches and do not depend on the external incident test hosts.

`TrustProbe` references the exact packaged `Sonarr.Common` and `Sonarr.Core`
assemblies. It calls the application's `ManagedHttpDispatcher` and
`X509CertificateValidationService`, with the normal enabled validation setting.
The release workflow overlays only the managed entry DLL in a disposable
container. It keeps the actual Sonarr apphost, bundled runtime, dependency
metadata, application client assemblies and image OS certificate store. No user configuration,
credentials, indexers, database or application service is touched.

A normal public HTTPS control must return HTTP 200. All twelve incident chains
must be rejected by the actual application's callback with an untrusted,
partial or revoked chain. DNS failures, timeouts, expiry and wrong-host errors
do not pass. The hosts are operated by the public incident reporter:
https://emudhra.we-have-a-problem.com/ .

If qualification fails, fix the certificate/client error or retry when the test
service recovers; do not disable validation. To recover a deployment, retain its
previous image digest, but do not roll back to an image that accepts these chains.

The standalone Node check is `node scripts/security/verify-node-trust.mjs` under
the same Node executable and trust environment as the build.
