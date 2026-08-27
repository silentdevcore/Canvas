import assert from 'node:assert/strict';
import test from 'node:test';
import { accountLocales } from '../src/authI18n.ts';
import { describeDesignerAccessError, designerAccessActionLabel } from '../src/designerAccessPresentation.ts';

test('expired Trials provide a subscription action instead of a generic error', () => {
  const result = describeDesignerAccessError({
    status: 403,
    code: 'PXA_TRIAL_EXPIRED',
    message: 'The Trial period has expired.',
  }, 'en');

  assert.equal(result.title, 'Your Designer trial has expired');
  assert.equal(result.primaryAction, 'subscription');
  assert.equal(result.secondaryAction, 'dashboard');
});

test('permission and organization denials provide actionable states', () => {
  const permission = describeDesignerAccessError(
    { status: 403, code: 'PXA_DESIGNER_PERMISSION_REQUIRED', message: '' }, 'en');
  assert.equal(permission.primaryAction, 'dashboard');
  assert.equal(permission.secondaryAction, 'support');
  assert.equal(
    describeDesignerAccessError({ status: 403, code: 'PXA_ORGANIZATION_INACTIVE', message: '' }, 'en').title,
    'Organization access unavailable',
  );
});

test('every Account locale has a localized Trial presentation', () => {
  for (const locale of accountLocales) {
    const result = describeDesignerAccessError({ status: 403, code: 'PXA_TRIAL_EXPIRED', message: '' }, locale);
    assert.ok(result.title.length > 10, locale);
    assert.ok(result.message.length > 20, locale);
    assert.ok(designerAccessActionLabel(result.primaryAction, locale).length > 2, locale);
  }
});

test('offline and invalid handoff states can be retried', () => {
  assert.equal(describeDesignerAccessError({ message: '', isOffline: true }, 'en').primaryAction, 'retry');
  assert.equal(describeDesignerAccessError({ status: 400, code: 'PXA_DESIGNER_HANDOFF_INVALID', message: '' }, 'en').primaryAction, 'retry');
});
