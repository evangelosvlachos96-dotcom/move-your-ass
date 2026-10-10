import { FormControl } from '@angular/forms';
import { describe, expect, it } from 'vitest';
import { emailAddress } from './email-rules';

describe('public email address validation', () => {
  it.each(['a@localhost', 'a@domain.', 'a@.com', 'a@b..com', 'a b@example.com', 'a@@example.com'])(
    'rejects %s',
    (value) => {
      expect(emailAddress(new FormControl(value))).toEqual({ email: true });
    },
  );
  it.each(['name@example.com', 'name+training@sub.example.gr'])('accepts %s', (value) => {
    expect(emailAddress(new FormControl(value))).toBeNull();
  });
});
