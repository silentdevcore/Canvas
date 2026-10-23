# PVADE Designer Project Opening

## Implemented Browser Application

The production Designer ships a web app manifest, install icons, network-only
service worker and File Handling API consumer. Supported Chromium installations
can offer `.pvadp` and `.pvads` to the installed PWA. This is not an Electron
application, command-line opener, or promise of support in every browser.

The icons are generic project-file icons, not approved corporate logo artwork.
The app requires HTTPS in production and a working same-origin API. There is no
offline document editing or background upload.

## Install And Validate

1. Deploy the production Designer with its manifest, icons and worker intact.
2. Check `/open-project` is routed to the SPA, including through the reverse proxy.
3. In a supported browser, install PVADE Designer using the browser's app action.
4. Approve browser/OS file handling only for the two project extensions.
5. Open a synthetic PVADP and PVADS through the OS, both signed in and signed out.
6. Confirm an existing edited window remains unchanged and the new window opens.
7. Decline permissions and confirm the manual project chooser remains available.
8. Uninstall through the browser/OS and confirm file associations are removed.

Record browser, OS, installed app identity and each result before declaring that
platform supported. Manifest validation or a simulated launch is not this test.
No registration commands are automatically executed by repository tooling.

## Authentication And Data Handling

`/open-project` is a public intake page, not a public editor. It holds a file
handle without reading its bytes. Sign-in opens a separate protected Designer
tab using the existing Account handoff. Return to intake and choose Check access.

Only an explicit Open action after authenticated permission checks reads the
file. The user and active organization are checked again before transfer. A
single-use random identifier transfers the file in memory to the protected editor;
neither a path nor filename is put in the URL. Transfers expire after five minutes
and are cleared by tenant/session cleanup. Reloading loses the transfer.

No source file is written to browser storage, CacheStorage or IndexedDB. The
service worker adds no cache. Normal project import still validates the archive,
assets, compatibility and import limits. PDF launch imports start a fresh document
instead of retaining a previously saved template's persistence identity.

The limit is one non-empty project of at most 25 MiB per window. Import, create
and product access permissions are all required. Server authorization remains
authoritative; the browser checks do not replace it.

Progress uploads identify the Designer and acquire a fresh CSRF token before
creating the upload request. A failed token request or cancelled operation must
not send the selected file. This also applies to the ordinary project chooser.

## Validation Boundaries

Automated tests cover unread anonymous intake, owner/tenant binding, expiry,
single use, cancellation, size and extension limits, and explicit opening.
Browser smoke tests use synthetic sessions and launch handles; they must be
reported separately from real installed-app and live authenticated API tests.

`nativeFileOpenSupported` remains false until real platform association tests pass;
`browserFileOpenImplemented` records implementation without implying certification.

## Platform References

- [Web app file handling](https://wicg.github.io/file-handling/explainer.html)
- [Chromium launch handling](https://developer.chrome.com/docs/web-platform/launch-handler)

The manifest requests a new client so a launch does not navigate an edited window.
Browser capabilities are detected at runtime; unsupported browsers retain the
manual project chooser.
