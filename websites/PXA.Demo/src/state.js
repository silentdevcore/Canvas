import { demos } from './demoData.js';

function clone(value) {
  return structuredClone(value);
}

function createInitialBookingState() {
  return clone(demos.find((demo) => demo.id === 'booking-receipt').preview);
}

let bookingState = createInitialBookingState();

export function getBookingState() {
  return bookingState;
}

export function updateBookingState(updater) {
  bookingState = updater(bookingState);
  return bookingState;
}

export function getActiveDemo() {
  const match = window.location.hash.match(/^#demo\/([^/]+)$/);
  const requestedDemo = match ? demos.find((demo) => demo.id === match[1]) : null;
  return requestedDemo || demos[0];
}
