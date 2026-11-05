import { describe, expect, it, vi } from 'vitest';

import { serverPartition, ServerViews, type ServerView, type ServerViewHost } from './serverViews';

const FAMILY = { id: 'family', url: 'https://family.example.org' };
const FRIENDS = { id: 'friends', url: 'https://friends.example.org' };

function fakeView(id: number) {
  let onLoaded: (() => void) | undefined;
  const view = {
    partition: '',
    visible: false,
    bounds: { x: 0, y: 0, width: 0, height: 0 },
    webContents: {
      id,
      loadURL: vi.fn(() => Promise.resolve()),
      once: vi.fn((_event: 'did-finish-load', listener: () => void) => {
        onLoaded = listener;
      }),
      focus: vi.fn(),
      reload: vi.fn(),
      toggleDevTools: vi.fn(),
      send: vi.fn(),
      isDestroyed: () => false,
      close: vi.fn(),
    },
    setBounds(bounds: { x: number; y: number; width: number; height: number }) {
      view.bounds = bounds;
    },
    setVisible(visible: boolean) {
      view.visible = visible;
    },
    loaded: () => onLoaded?.(),
  };
  return view;
}

function setUp(servers = [FAMILY, FRIENDS]) {
  const views: ReturnType<typeof fakeView>[] = [];
  let size = { width: 1024, height: 768 };
  let onResize: () => void = () => undefined;
  let onClosed: () => void = () => undefined;
  const host: ServerViewHost = {
    addView: vi.fn(),
    contentSize: () => size,
    onResize: (listener) => {
      onResize = listener;
    },
    onClosed: (listener) => {
      onClosed = listener;
    },
  };
  const onFirstShown = vi.fn();
  const serverViews = new ServerViews({
    host,
    servers,
    createView: (partition): ServerView => {
      const view = fakeView(views.length + 1);
      view.partition = partition;
      views.push(view);
      return view;
    },
    url: 'voyager://app/index.html',
    onFirstShown,
    onLoadFailure: vi.fn(),
  });
  return {
    serverViews,
    views,
    onFirstShown,
    resize: (width: number, height: number) => {
      size = { width, height };
      onResize();
    },
    close: () => {
      onClosed();
    },
  };
}

describe('serverPartition', () => {
  it('gives each server a session of its own, and one apart when there is none', () => {
    expect(serverPartition('family')).toBe('voyager-server-family');
    expect(serverPartition('friends')).not.toBe(serverPartition('family'));
    expect(serverPartition(undefined)).toBe('voyager-no-server');
  });
});

describe('ServerViews', () => {
  it('creates one view per server, each in its own session, and shows the first', () => {
    const { views, serverViews } = setUp();

    expect(views.map((view) => view.partition)).toEqual(['voyager-server-family', 'voyager-server-friends']);
    expect(views.map((view) => view.visible)).toEqual([true, false]);
    expect(serverViews.selected).toBe('family');
  });

  it('knows which server a page belongs to, and no other page', () => {
    const { views, serverViews } = setUp();

    expect(serverViews.serverOf(views[1]?.webContents.id ?? 0)).toEqual(FRIENDS);
    expect(serverViews.owns(99)).toBe(false);
  });

  it('shows the server chosen, and refuses one it does not know', () => {
    const { views, serverViews } = setUp();

    expect(serverViews.select('friends')).toBe(true);
    expect(views.map((view) => view.visible)).toEqual([false, true]);
    expect(views[1]?.webContents.focus).toHaveBeenCalled();
    expect(serverViews.select('elsewhere')).toBe(false);
    expect(serverViews.selected).toBe('friends');
  });

  it('opens the window once the view shown has loaded its page', () => {
    const { views, onFirstShown } = setUp();

    views[1]?.loaded();
    expect(onFirstShown).not.toHaveBeenCalled();
    views[0]?.loaded();
    expect(onFirstShown).toHaveBeenCalledOnce();
  });

  it('has one view, in a session apart, when the app knows no server', () => {
    const { views, serverViews } = setUp([]);

    expect(views.map((view) => view.partition)).toEqual(['voyager-no-server']);
    expect(serverViews.serverOf(views[0]?.webContents.id ?? 0)).toBeUndefined();
    expect(serverViews.owns(views[0]?.webContents.id ?? 0)).toBe(true);
  });

  it('keeps every view filling the window, and closes their pages with it', () => {
    const { views, resize, close } = setUp();

    resize(800, 600);
    expect(views.map((view) => view.bounds)).toEqual([
      { x: 0, y: 0, width: 800, height: 600 },
      { x: 0, y: 0, width: 800, height: 600 },
    ]);
    close();
    expect(views.every((view) => vi.mocked(view.webContents.close).mock.calls.length === 1)).toBe(true);
  });

  it('gives the keyboard, the reload and the developer tools to the view shown', () => {
    const { views, serverViews } = setUp();
    serverViews.select('friends');
    vi.mocked(views[1]?.webContents.focus ?? vi.fn()).mockClear();

    serverViews.focusSelected();
    serverViews.reloadSelected();
    serverViews.toggleDeveloperToolsOfSelected();

    expect(views[1]?.webContents.focus).toHaveBeenCalledOnce();
    expect(views[1]?.webContents.reload).toHaveBeenCalledOnce();
    expect(views[1]?.webContents.toggleDevTools).toHaveBeenCalledOnce();
    expect(views[0]?.webContents.reload).not.toHaveBeenCalled();
  });
});
