import { createPublicKey, verify } from 'node:crypto';

const date = value => typeof value === 'string' && !value.startsWith('0000-') && /^\d{4}-\d{2}-\d{2}$/.test(value)
  && !Number.isNaN(Date.parse(value)) && new Date(value).toISOString().slice(0, 10) === value;
const decode = value => {
  if (!/^[\w-]+$/.test(value)) throw new Error('Invalid base64url');
  const bytes = Buffer.from(value, 'base64url');
  if (bytes.toString('base64url') !== value) throw new Error('Invalid base64url');
  return bytes;
};

/** Pure offline verification in Node/Electron. No IPC, HTTP or .NET runtime. */
export function verifyLicense(token, { publicKey, issuer, product, buildDate }) {
  if (!date(buildDate)) throw new Error('Build date must be YYYY-MM-DD');
  if (!token?.trim()) return { valid: false, status: 'missing', license: null };
  try {
    token = token.trim();
    if (token.length > 16000) throw new Error('Too large');
    const parts = token.split('.');
    if (parts.length !== 3) throw new Error('Expected JWT');
    const header = JSON.parse(decode(parts[0]).toString('utf8'));
    if (header.alg !== 'ES256' || header.typ !== 'JWT' || Object.keys(header).some(k => !['alg','typ'].includes(k))) throw new Error('Unsupported header');
    const key = createPublicKey(publicKey);
    if (key.asymmetricKeyType !== 'ec' || key.asymmetricKeyDetails.namedCurve !== 'prime256v1') throw new Error('Expected P-256');
    const sig = decode(parts[2]);
    if (sig.length !== 64 || !verify('sha256', Buffer.from(parts[0] + '.' + parts[1], 'ascii'),
      { key, dsaEncoding: 'ieee-p1363' }, sig)) throw new Error('Invalid signature');
    const c = JSON.parse(decode(parts[1]).toString('utf8'));
    if (typeof c.iss !== 'string' || !c.iss.trim() || c.iss.length > 200 || typeof c.aud !== 'string' || !c.aud.trim() || c.aud.length > 80 || c.iss !== issuer || c.aud !== product || typeof c.sub !== 'string' || !/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(c.sub)
      || typeof c.name !== 'string' || !c.name.trim() || c.name.length > 200
      || (c.organization != null && (typeof c.organization !== 'string' || c.organization.length > 200))
      || !Number.isInteger(c.seats) || c.seats < 1 || c.seats > 1000000
      || !Number.isSafeInteger(c.iat) || c.iat < 0 || c.iat > 253402300799
      || !['Free','Pro','Enterprise'].includes(c.edition) || typeof c.lifetime !== 'boolean'
      || ('exp' in c) || ('nbf' in c)
      || (c.lifetime ? c.updatesThrough != null : !date(c.updatesThrough))) throw new Error('Invalid claims');
    const status = !['Pro','Enterprise'].includes(c.edition) ? 'editionNotCovered'
      : c.lifetime || buildDate <= c.updatesThrough ? 'valid' : 'buildNotCovered';
    return { valid: status === 'valid', status, license: c };
  } catch { return { valid: false, status: 'invalid', license: null }; }
}
