const test = require('node:test');
const assert = require('node:assert/strict');

const { API_CHANNELS, EVENT_CHANNELS } = require('../ipcDefinitions.cjs');

test('API channels include required request-response entries', () => {
  assert.ok(API_CHANNELS.includes('showOpenDialog'));
  assert.ok(API_CHANNELS.includes('invokeCommand'));
});

test('event channels include backend lifecycle notifications', () => {
  assert.ok(EVENT_CHANNELS.includes('backendReady'));
  assert.ok(EVENT_CHANNELS.includes('backendCrashed'));
  assert.ok(EVENT_CHANNELS.includes('backendFailed'));
});

test('channel collections are immutable', () => {
  assert.equal(Object.isFrozen(API_CHANNELS), true);
  assert.equal(Object.isFrozen(EVENT_CHANNELS), true);
});

