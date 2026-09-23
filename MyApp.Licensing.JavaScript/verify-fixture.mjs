import { verifyLicense } from './license.mjs';
let input = '';
for await (const chunk of process.stdin) input += chunk;
const { token, ...options } = JSON.parse(input);
process.stdout.write(JSON.stringify(verifyLicense(token, options)));
