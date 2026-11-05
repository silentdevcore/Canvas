import assert from 'node:assert/strict';

export const screenshotNames = ['designer', 'account', 'admin'].flatMap(site =>
  ['desktop', 'mobile'].map(size => `${site}-https-${size}.png`));

export function safeFailureEvidence(error, secrets = []) {
  const redact = value => {
    let text = String(value);
    for (const secret of secrets.filter(Boolean)) {
      text = text.replaceAll(secret, '[redacted]').replaceAll(encodeURIComponent(secret), '[redacted]');
    }
    return text.replace(/https?:\/\/[^\s"'<>]+/g, value => {
      try {
        const url = new URL(value); url.search = ''; url.hash = ''; url.username = ''; url.password = '';
        return url.href;
      } catch { return '[redacted-url]'; }
    });
  };
  return { type: /^[A-Za-z]+Error$/.test(error?.name) ? error.name : 'Error',
    message: redact(error?.message).slice(0, 2000),
    frames: String(error?.stack).split('\n').filter(value => /^\s+at /.test(value)).slice(0, 16)
      .map(value => redact(value).slice(0, 500)) };
}

export function validateBrowserEvidence(result) {
  assert.equal(result.checks?.length, 12, 'Every HTTPS browser scenario must complete.');
  assert.equal(new Set(result.checks).size, 12, 'Scenario evidence must be unique.');
  assert.ok(result.checks.every(value => /^[a-z]+(?:-[a-z]+)*$/.test(value)));
  assert.deepEqual(result.screenshots, screenshotNames);
  assert.equal(result.tlsValidationDisabled, false);
  assert.equal(result.apiMocking, false);
  assert.equal(result.hostOnlyCookiesVerified, true);
  assert.equal(result.platform, 'isolated-linux-chromium');
}
