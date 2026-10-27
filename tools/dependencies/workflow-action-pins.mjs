export const requiredActionPins = new Map([
  ['actions/checkout', '3d3c42e5aac5ba805825da76410c181273ba90b1'],
  ['actions/setup-node', '949feb2413d6458794dcd2491c4babbbce0c15c1'],
  ['actions/setup-dotnet', 'a98b56852c35b8e3190ac28c8c2271da59106c68'],
  ['actions/upload-artifact', 'cf430e030ddbb5b0abf93d22962f4752f3646cd9'],
  ['docker/setup-buildx-action', 'f87e5991a6d7451dcb8d9637bfbc97413f497069'],
  ['docker/login-action', '81c6f93d4bc991e510b342a00b419b3bb4efc5c6'],
  ['docker/build-push-action', 'c3c9e263c25d99ce0380d002d59b67737d91b0dc'],
]);

export function validateWorkflowActionPins(sources) {
  const seen = new Set();
  for (const { name, source } of sources) {
    for (const [, action, reference] of source.matchAll(/^\s*(?:-\s*)?uses:\s*([^@\s]+)@([^\s#]+)/gmu)) {
      if (action.startsWith('./')) continue;
      if (!/^[a-f0-9]{40}$/u.test(reference)) {
        throw new Error(`${name} must pin ${action} to a full commit SHA`);
      }
      const expected = requiredActionPins.get(action);
      if (expected && reference !== expected) {
        throw new Error(`${name} has an unexpected ${action} pin; expected ${expected}`);
      }
      seen.add(action);
    }
  }
  for (const action of requiredActionPins.keys()) {
    if (!seen.has(action)) throw new Error(`Required action pin is missing: ${action}`);
  }
}
