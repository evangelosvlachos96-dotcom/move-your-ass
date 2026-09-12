# 07 — Manual Test Checklist

ADR-014 defers automated tests. This is what replaces them. Re-run the whole thing at the end of
every frontend phase — it takes about fifteen minutes and it is the only regression net there is.

Mark anything that fails and fix it before starting the next phase.

---

## Setup

Two terminals:

```powershell
dotnet run --project src/Mya.Api      # terminal 1
cd web ; npm start                    # terminal 2
```

Then create the test accounts once, via Swagger at `http://localhost:5077/swagger`. Log in as the
seeded admin first and paste the token into the **Authorize** button.

| Account | How | Purpose |
|---|---|---|
| `pending@test.gr` | register only | ACCOUNT_PENDING path |
| `active@test.gr` | register, then approve | normal client |
| `declined@test.gr` | register, then decline | ACCOUNT_DECLINED path |
| `suspended@test.gr` | register, approve, then suspend | ACCOUNT_SUSPENDED path |
| `temp@test.gr` | `POST /api/admin/users` | MUST_CHANGE_PASSWORD path — save the temp password |

Use the same password everywhere, e.g. `TestPass2026x`.

---

## A. Brand and assets

- [ ] Favicon shows in the browser tab, not the default Angular one
- [ ] Toolbar shows the horizontal logo, not a broken-image icon
- [ ] Login page shows the stacked wordmark above the form
- [ ] DevTools → Network, filter Img: no 404s
- [ ] DevTools → Application → Manifest: loads, name "MoveYourAss", icons listed, no errors
- [ ] Tab title reads "MoveYourAss"
- [ ] Nothing on screen is volt except the primary button, the active nav item, and focus rings

## B. Login form validation

The point of this group is that the form should not shout at someone who has not done anything wrong.

- [ ] Click into email, then into password, then click the page background — **neither field is red**
- [ ] Click Submit with both empty — both go red, both show Greek messages, focus jumps to email
- [ ] Type `giannis@` and tab away — red **immediately**, message says the email is invalid
- [ ] Keep typing to `giannis@test.gr` — the error clears **as you type**, not on blur
- [ ] Error text is always present next to the red, never colour alone
- [ ] Enter key submits the form
- [ ] Submit button is disabled while the request is in flight (watch on a slow network throttle)
- [ ] Double-clicking Submit does not fire two login requests

## C. Core auth — the ones that matter most

- [ ] Log in as admin → lands on dashboard, shows your name and "Διαχειριστής"
- [ ] **F5 → still logged in.** Network shows one `POST /auth/refresh` then `GET /auth/me`
- [ ] DevTools → Application → Local Storage: **empty**. Session Storage: **empty**
- [ ] Cookies → `rt` exists, flagged HttpOnly and Secure, path `/api/auth`
- [ ] `document.cookie` in the console returns an empty string (proves HttpOnly works)
- [ ] Log out → back on login. F5 → stays on login, no ghost session
- [ ] While logged out, type `/dashboard` in the address bar → redirected to login
- [ ] While logged in, type `/login` → redirected to dashboard

## D. Error code routing

Each of these proves one branch of the error interceptor.

- [ ] `pending@test.gr` → lands on the pending page, not a generic error
- [ ] `declined@test.gr` → Greek snackbar explaining the registration was not accepted
- [ ] `suspended@test.gr` → Greek snackbar, stays on login
- [ ] `temp@test.gr` with the temp password → forced to the change-password page
- [ ] From there, try navigating to `/dashboard` manually → bounced back to change-password
- [ ] Wrong password on a valid account → "λάθος στοιχεία", no stack trace, no crash
- [ ] Unknown email → **the same message as wrong password** (must not reveal which accounts exist)

## E. Single active session

This is the feature most likely to be quietly broken, because it only shows up across two browsers.

- [ ] Log in as `active@test.gr` in Chrome
- [ ] Log in as the same user in an incognito window
- [ ] Back in the first window, click around or wait for the token to expire (15 min — or restart
      the API to force it)
- [ ] First window logs out with "Έγινε αποσύνδεση από την άλλη συσκευή"

## F. Responsive

Resize with DevTools device toolbar, not by guessing.

- [ ] 1280px — sidenav open, side mode, no hamburger button
- [ ] 960px — the switch point, check nothing overlaps mid-transition
- [ ] 600px — sidenav closed, hamburger appears, tapping it opens the drawer
- [ ] 375px (iPhone SE) — login form fits, no horizontal scroll, inputs are tappable
- [ ] 375px — toolbar logo does not overflow or collide with the user menu
- [ ] Rotate to landscape on a phone size — still usable

## G. Console hygiene

- [ ] No red errors in the console during a full login → reload → logout cycle
- [ ] No Angular hydration or change-detection warnings
- [ ] The only 401s in the Network tab are the expected silent-refresh attempts before login

---

## Known acceptable noise

Things that look wrong but are not, so nobody wastes time on them:

- `CORS policy execution failed` in the API log when calling from Swagger — Swagger is on port
  5077, the allowed origin is 4200. Same-origin requests do not need CORS and proceed anyway.
- The AutoMapper licence warning at startup, until a Community key is registered.
- Two 401s on first page load, from the silent refresh attempt when there is no session yet.

---

## After each run

Anything that failed and is not being fixed immediately goes in `docs/backlog.md` with a date.
A checklist with permanently unticked boxes stops being read.
