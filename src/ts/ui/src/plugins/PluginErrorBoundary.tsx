// Catches a plugin panel that crashes while it is drawn, so that the rest of the page stays on screen (ADR-0015, decision 4).
import { Component, type ReactNode } from 'react';

export interface PluginErrorBoundaryProps {
  /** What to draw in place of the panel once it has crashed. */
  readonly fallback: ReactNode;
  /** Called once when the panel crashes, so that the plugin can be shown as being in error. */
  readonly onError: () => void;
  readonly children: ReactNode;
}

interface PluginErrorBoundaryState {
  readonly failed: boolean;
}

/** Draws its children; once one of them throws while drawing, draws the fallback instead. */
export class PluginErrorBoundary extends Component<PluginErrorBoundaryProps, PluginErrorBoundaryState> {
  public override state: PluginErrorBoundaryState = { failed: false };

  public static getDerivedStateFromError(): PluginErrorBoundaryState {
    return { failed: true };
  }

  public override componentDidCatch(): void {
    // The error itself is not kept: it comes from a plugin and may hold a path, so the page does not show or send it.
    this.props.onError();
  }

  public override render(): ReactNode {
    return this.state.failed ? this.props.fallback : this.props.children;
  }
}
