// Run with the same Node executable and trust settings as the application.
// A network/DNS failure is never accepted as proof of certificate distrust.
import https from 'node:https';

const control = 'https://www.google.com/';
const incident = ['dv', 'ov', 'ev'].flatMap(kind => ['g3b', 'g3c'].flatMap(generation =>
  ['', '-aaa'].map(cross => `https://${kind}-${generation}${cross}-emudhra.we-have-a-problem.com/`)));
const rejected = new Set(['UNABLE_TO_GET_ISSUER_CERT_LOCALLY', 'UNABLE_TO_VERIFY_LEAF_SIGNATURE', 'CERT_REVOKED', 'SELF_SIGNED_CERT_IN_CHAIN']);
console.log(JSON.stringify({ node: process.version, options: process.env.NODE_OPTIONS, caFile: process.env.SSL_CERT_FILE }));
let passed = true;
for (const url of [control, ...incident]) {
  const result = await new Promise(resolve => {
    const request = https.get(url, { timeout: 15000 }, response => {
      response.resume();
      resolve({ url, status: response.statusCode, authorized: response.socket.authorized });
    });
    request.on('timeout', () => request.destroy(new Error('request timeout')));
    request.on('error', error => resolve({ url, error: error.code || error.message }));
  });
  result.pass = url === control ? result.status === 200 && result.authorized === true : rejected.has(result.error);
  console.log(JSON.stringify(result));
  passed &&= result.pass;
}
if (!passed) process.exitCode = 1;
