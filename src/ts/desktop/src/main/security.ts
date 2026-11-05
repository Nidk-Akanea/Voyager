// Electron hardening (docs/conventions/typescript.md): windows only ever show the app's own pages,
// open nothing else and get no device or browser permission.
import { app, session, type Session } from 'electron';

/** Applies to every window and frame the app will create. */
export function lockDownWebContents(isAllowedPage: (url: string) => boolean): void {
  app.on('web-contents-created', (_event, contents) => {
    contents.setWindowOpenHandler(() => ({ action: 'deny' }));
    contents.on('will-navigate', (event) => {
      if (!isAllowedPage(event.url)) {
        event.preventDefault();
      }
    });
    contents.on('will-redirect', (event) => {
      if (!isAllowedPage(event.url)) {
        event.preventDefault();
      }
    });
    contents.on('will-attach-webview', (event) => {
      event.preventDefault();
    });
  });
}

/** Camera, microphone, location, notifications…: Voyager needs none of them, in any of its sessions. */
export function denyAllPermissions(target: Session = session.defaultSession): void {
  target.setPermissionRequestHandler((_contents, _permission, callback) => {
    callback(false);
  });
  target.setPermissionCheckHandler(() => false);
}
