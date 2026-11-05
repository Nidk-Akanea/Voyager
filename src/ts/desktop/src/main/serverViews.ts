// One view per group server in the main window (spec of the switch to client-server, "Les serveurs enregistrés";
// ADR-0019, decision 4): each view lives in a session of its own, whose protocol only forwards to its server, so the
// interface modules of one server can never call another. One view is shown at a time; the page asks for another with
// the server selector.
import type { ServerEntry } from './serverRegistry';

/** The in-memory session of the view of a server (nothing of it is kept on disk); `undefined` when no server is known. */
export function serverPartition(serverId: string | undefined): string {
  return serverId === undefined ? 'voyager-no-server' : `voyager-server-${serverId}`;
}

/** The part of a view this module drives: an Electron WebContentsView in the app, a fake in tests. */
export interface ServerView {
  readonly webContents: {
    readonly id: number;
    loadURL(url: string): Promise<void>;
    once(event: 'did-finish-load', listener: () => void): unknown;
    focus(): void;
    reload(): void;
    toggleDevTools(): void;
    send(channel: string, payload: unknown): void;
    isDestroyed(): boolean;
    close(): void;
  };
  setBounds(bounds: { x: number; y: number; width: number; height: number }): void;
  setVisible(visible: boolean): void;
}

/** The part of the window this module drives. */
export interface ServerViewHost {
  addView(view: ServerView): void;
  /** The area the views fill, inside the window's frame. */
  contentSize(): { width: number; height: number };
  onResize(listener: () => void): void;
  onClosed(listener: () => void): void;
}

export interface ServerViewsOptions {
  readonly host: ServerViewHost;
  /** The servers the app knows; none gives one view that says so. */
  readonly servers: readonly ServerEntry[];
  /** Creates the view of a server, in the session named by `partition`. */
  readonly createView: (partition: string) => ServerView;
  /** The page every view loads: the main screen of the user interface. */
  readonly url: string;
  /** Called once, when the first view shown has loaded its page (or failed to). */
  readonly onFirstShown: () => void;
  /** A view could not load its page; `serverId` is undefined for the view of no server. */
  readonly onLoadFailure: (serverId: string | undefined, error: unknown) => void;
}

/** The views of the main window, one per server, and which one is shown. */
export class ServerViews {
  readonly #views = new Map<string | undefined, ServerView>();
  readonly #serverByContents = new Map<number, ServerEntry | undefined>();
  readonly #host: ServerViewHost;
  #selected: string | undefined;

  public constructor(options: ServerViewsOptions) {
    this.#host = options.host;
    const entries: readonly (ServerEntry | undefined)[] = options.servers.length === 0 ? [undefined] : options.servers;
    this.#selected = entries[0]?.id;

    let shown = false;
    const firstShown = (): void => {
      if (!shown) {
        shown = true;
        options.onFirstShown();
      }
    };
    for (const server of entries) {
      const view = options.createView(serverPartition(server?.id));
      this.#views.set(server?.id, view);
      this.#serverByContents.set(view.webContents.id, server);
      options.host.addView(view);
      const isFirst = server?.id === this.#selected;
      if (isFirst) {
        view.webContents.once('did-finish-load', firstShown);
      }
      view.webContents.loadURL(options.url).catch((error: unknown) => {
        options.onLoadFailure(server?.id, error);
        if (isFirst) {
          firstShown();
        }
      });
    }

    this.#layout();
    options.host.onResize(() => {
      this.#layout();
    });
    options.host.onClosed(() => {
      for (const view of this.#views.values()) {
        if (!view.webContents.isDestroyed()) {
          view.webContents.close();
        }
      }
    });
  }

  /** The server a page belongs to; undefined for the view of no server, and for a page that is not one of these views. */
  public serverOf(contentsId: number): ServerEntry | undefined {
    return this.#serverByContents.get(contentsId);
  }

  /** Whether a page is one of these views. */
  public owns(contentsId: number): boolean {
    return this.#serverByContents.has(contentsId);
  }

  /** The identifier of the server shown. */
  public get selected(): string | undefined {
    return this.#selected;
  }

  /** Shows the view of a server; false when the app does not know it. */
  public select(serverId: string): boolean {
    const view = this.#views.get(serverId);
    if (view === undefined) {
      return false;
    }
    this.#selected = serverId;
    this.#layout();
    view.webContents.focus();
    return true;
  }

  /** Gives the keyboard to the view shown: the window has no page of its own. */
  public focusSelected(): void {
    const view = this.#views.get(this.#selected);
    if (view !== undefined && !view.webContents.isDestroyed()) {
      view.webContents.focus();
    }
  }

  /** Reloads the page of the view shown (development). */
  public reloadSelected(): void {
    this.#views.get(this.#selected)?.webContents.reload();
  }

  /** Opens or closes the developer tools of the view shown (development). */
  public toggleDeveloperToolsOfSelected(): void {
    this.#views.get(this.#selected)?.webContents.toggleDevTools();
  }

  /** Sends a message to every view still there. */
  public send(channel: string, payload: unknown): void {
    for (const view of this.#views.values()) {
      if (!view.webContents.isDestroyed()) {
        view.webContents.send(channel, payload);
      }
    }
  }

  // Every view fills the window; only the one shown is visible.
  #layout(): void {
    const { width, height } = this.#host.contentSize();
    for (const [serverId, view] of this.#views) {
      view.setBounds({ x: 0, y: 0, width, height });
      view.setVisible(serverId === this.#selected);
    }
  }
}
