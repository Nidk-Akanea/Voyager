// The confirmations of the plugin management (ADR-0016, decision 5): native dialogs of the main process, which the
// module of a plugin cannot confirm in the user's place. "Cancel" is the default, and installing a plugin nobody
// verified waits for "I understand" to be ticked. Their texts are Fluent messages of the main process (ADR-0008),
// in src/main/i18n/<language>/plugins.ftl.
import { FluentBundle, FluentResource, type FluentVariable } from '@fluent/bundle';

import enPlugins from '../i18n/en/plugins.ftl?raw';
import frPlugins from '../i18n/fr/plugins.ftl?raw';
import { cleanDisplayText } from './displayText';

/** The languages of the dialogs; French when the system prefers none of them, as the user interface does. */
export type DialogLocale = 'fr' | 'en';

/** The message files of the dialogs, by language: the same messages in both. */
export const DIALOG_MESSAGE_FILES: Readonly<Record<DialogLocale, string>> = { fr: frPlugins, en: enPlugins };

/** What installing a package would do, compared with the plugin already installed under its identifier. */
export type InstallKind = 'new' | 'upgrade' | 'downgrade' | 'reinstall';

/**
 * Who vouches for a plugin: the application for a shipped one, a known key for a verified package (none until a
 * signature key is decided, Q31), nobody otherwise.
 */
export type Verification = 'guaranteed' | 'verified' | 'unverified';

/** A contract of the package, with whether the application will stop supporting its version. */
export interface ContractFact {
  readonly contract: string;
  readonly version: number;
  readonly role: 'provides' | 'requires';
  readonly deprecated: boolean;
}

/** What the confirmation of an installation shows; the texts come from the package and are cleaned here. */
export interface InstallFacts {
  /** The name of the file the user chose. */
  readonly fileName: string;
  readonly sha256: string;
  readonly id: string;
  readonly version: string;
  readonly contracts: readonly ContractFact[];
  readonly hasDotnet: boolean;
  readonly hasUi: boolean;
  readonly verification: Verification;
  readonly duplicateOfShipped: boolean;
  /** The version already installed, for a replacement. */
  readonly installedVersion?: string;
  readonly kind: InstallKind;
}

/** A plugin a confirmation names. */
export interface PluginFacts {
  readonly id: string;
  readonly version: string;
  readonly verification: Verification;
}

/** The content of a native dialog: the options of Electron's dialog.showMessageBox, and whether the box must be ticked. */
export interface ConfirmationBox {
  readonly type: 'warning' | 'question';
  readonly title: string;
  readonly message: string;
  readonly detail: string;
  /** "Cancel" first: it is the default, and what closing the dialog answers. */
  readonly buttons: readonly [cancel: string, confirm: string];
  readonly defaultId: 0;
  readonly cancelId: 0;
  readonly noLink: true;
  readonly checkboxLabel?: string;
  readonly checkboxChecked?: false;
  /** Whether the confirmation counts only once the box is ticked. */
  readonly requiresCheckbox: boolean;
  /** What to say when the user confirmed without ticking the box. */
  readonly checkboxReminder?: string;
}

/** What the user answered: the index of the button, and whether the box was ticked. */
export interface DialogAnswer {
  readonly response: number;
  readonly checkboxChecked: boolean;
}

function createBundle(locale: DialogLocale): FluentBundle {
  // No isolation marks around the values: a native dialog would show them, and the values are cleaned already.
  const bundle = new FluentBundle(locale, { useIsolating: false });
  const errors = bundle.addResource(new FluentResource(DIALOG_MESSAGE_FILES[locale]));
  if (errors.length > 0) {
    throw new Error(`The ${locale} messages of the plugin dialogs do not parse: ${String(errors[0])}`);
  }
  return bundle;
}

const BUNDLES: Readonly<Record<DialogLocale, FluentBundle>> = { fr: createBundle('fr'), en: createBundle('en') };

/** A message of the dialogs in a language, or in French when that language lacks it. */
function message(locale: DialogLocale, id: string, variables?: Record<string, FluentVariable>): string {
  for (const bundle of [BUNDLES[locale], BUNDLES.fr]) {
    const pattern = bundle.getMessage(id)?.value;
    if (pattern !== undefined && pattern !== null) {
      return bundle.formatPattern(pattern, variables);
    }
  }
  throw new Error(`The message ${id} of the plugin dialogs is missing.`);
}

/** Picks the language of the dialogs among the user's preferences, or French, as the user interface does. */
export function pickDialogLocale(preferredLanguages: readonly string[]): DialogLocale {
  for (const language of preferredLanguages) {
    const base = language.toLowerCase().split('-')[0];
    if (base === 'fr' || base === 'en') {
      return base;
    }
  }
  return 'fr';
}

function contractList(locale: DialogLocale, contracts: readonly ContractFact[], role: ContractFact['role']): string {
  const listed = contracts
    .filter((contract) => contract.role === role)
    .map((contract) => `${cleanDisplayText(contract.contract)} v${String(contract.version)}`);
  return listed.length === 0 ? message(locale, 'plugins-dialog-none') : listed.join(', ');
}

function parts(hasDotnet: boolean, hasUi: boolean): string {
  if (hasDotnet && hasUi) {
    return 'dotnet-and-ui';
  }
  return hasDotnet ? 'dotnet' : 'ui';
}

function question(locale: DialogLocale, facts: InstallFacts, id: string, version: string): string {
  const from = cleanDisplayText(facts.installedVersion ?? '');
  switch (facts.kind) {
    case 'upgrade':
      return message(locale, 'plugins-dialog-upgrade-question', { id, from, to: version });
    case 'downgrade':
      return message(locale, 'plugins-dialog-downgrade-question', { id, from, to: version });
    case 'reinstall':
      return message(locale, 'plugins-dialog-reinstall-question', { id, version });
    case 'new':
      return message(locale, 'plugins-dialog-install-question', { id });
  }
}

/**
 * What every confirmation shares: the title, "Cancel" first and by default, the details ending on when the change
 * applies, and no box to tick.
 */
function confirmationBox(
  locale: DialogLocale,
  content: { type: ConfirmationBox['type']; question: string; details: readonly string[]; confirmLabel: string },
): ConfirmationBox {
  return {
    type: content.type,
    title: message(locale, 'plugins-dialog-title'),
    message: content.question,
    detail: [...content.details, '', message(locale, 'plugins-dialog-applied-at-restart')].join('\n'),
    buttons: [message(locale, 'plugins-dialog-cancel'), message(locale, content.confirmLabel)],
    defaultId: 0,
    cancelId: 0,
    noLink: true,
    requiresCheckbox: false,
  };
}

/** The same confirmation, counting only once "I understand" is ticked. */
function withUnderstanding(box: ConfirmationBox, locale: DialogLocale): ConfirmationBox {
  return {
    ...box,
    checkboxLabel: message(locale, 'plugins-dialog-understand'),
    checkboxChecked: false,
    requiresCheckbox: true,
    checkboxReminder: message(locale, 'plugins-dialog-understand-reminder'),
  };
}

const CONFIRM_LABELS: Readonly<Record<InstallKind, string>> = {
  new: 'plugins-dialog-install',
  reinstall: 'plugins-dialog-reinstall',
  upgrade: 'plugins-dialog-replace',
  downgrade: 'plugins-dialog-replace',
};

/**
 * The confirmation of an installation: the question, then the warning, the file, its fingerprint, the identifier,
 * the version, the contracts, what the plugin brings and its verification, in that order (spec, "Installer"). Only
 * a verified package goes without the warning and the box.
 */
export function installConfirmation(facts: InstallFacts, locale: DialogLocale): ConfirmationBox {
  const id = cleanDisplayText(facts.id);
  const version = cleanDisplayText(facts.version);
  const verified = facts.verification === 'verified';
  const deprecated = facts.contracts.filter((contract) => contract.deprecated);
  const notes = [
    ...(facts.duplicateOfShipped ? [message(locale, 'plugins-dialog-duplicate-of-shipped')] : []),
    ...deprecated.map((contract) =>
      message(locale, 'plugins-dialog-deprecated', {
        contract: cleanDisplayText(contract.contract),
        version: String(contract.version),
      }),
    ),
  ];
  const details = [
    ...(verified ? [] : [message(locale, 'plugins-dialog-warning')]),
    ...(facts.kind !== 'new' && !verified ? [message(locale, 'plugins-dialog-author-not-proven')] : []),
    '',
    message(locale, 'plugins-dialog-file', { name: cleanDisplayText(facts.fileName) }),
    message(locale, 'plugins-dialog-fingerprint', { sha256: facts.sha256 }),
    message(locale, 'plugins-dialog-identifier', { id }),
    message(locale, 'plugins-dialog-version', { version }),
    message(locale, 'plugins-dialog-provides', { contracts: contractList(locale, facts.contracts, 'provides') }),
    message(locale, 'plugins-dialog-requires', { contracts: contractList(locale, facts.contracts, 'requires') }),
    message(locale, 'plugins-dialog-brings', { parts: parts(facts.hasDotnet, facts.hasUi) }),
    message(locale, 'plugins-dialog-verification', { verification: facts.verification }),
    ...(notes.length > 0 ? ['', ...notes] : []),
  ];
  const box = confirmationBox(locale, {
    type: 'warning',
    question: question(locale, facts, id, version),
    details,
    confirmLabel: CONFIRM_LABELS[facts.kind],
  });
  return verified ? box : withUnderstanding(box, locale);
}

/** The confirmation of an uninstallation. */
export function uninstallConfirmation(plugin: PluginFacts, locale: DialogLocale): ConfirmationBox {
  return confirmationBox(locale, {
    type: 'question',
    question: message(locale, 'plugins-dialog-uninstall-question', {
      id: cleanDisplayText(plugin.id),
      version: cleanDisplayText(plugin.version),
    }),
    details: [message(locale, 'plugins-dialog-uninstall-detail')],
    confirmLabel: 'plugins-dialog-uninstall',
  });
}

/**
 * The confirmation of enabling a plugin: it lets code run again, without the box an installation needs; with it, and a
 * reminder of the suspicion, for a plugin suspended after a crash.
 */
export function enableConfirmation(plugin: PluginFacts, locale: DialogLocale, suspended = false): ConfirmationBox {
  const names = { id: cleanDisplayText(plugin.id), version: cleanDisplayText(plugin.version) };
  if (!suspended) {
    return confirmationBox(locale, {
      type: 'question',
      question: message(locale, 'plugins-dialog-enable-question', names),
      details: [
        message(locale, 'plugins-dialog-enable-detail'),
        message(locale, 'plugins-dialog-verification', { verification: plugin.verification }),
      ],
      confirmLabel: 'plugins-dialog-enable',
    });
  }
  // A plugin suspected of having crashed Voyager: the dialog recalls it, and waits for "I understand" (spec, rule 4 of
  // "Quand Voyager plante à cause d'un plugin").
  return withUnderstanding(
    confirmationBox(locale, {
      type: 'warning',
      question: message(locale, 'plugins-dialog-reenable-question', names),
      details: [
        message(locale, 'plugins-dialog-suspicion'),
        message(locale, 'plugins-dialog-reenable-detail'),
        message(locale, 'plugins-dialog-verification', { verification: plugin.verification }),
      ],
      confirmLabel: 'plugins-dialog-reenable',
    }),
    locale,
  );
}

/** The title of the dialog that chooses a package file, and the name of its filter. */
export function packageFileDialog(locale: DialogLocale): { readonly title: string; readonly filterName: string } {
  return {
    title: message(locale, 'plugins-dialog-choose-package'),
    filterName: message(locale, 'plugins-dialog-package-files'),
  };
}

/**
 * Asks the user until they cancel, or confirm as the box requires: a native dialog cannot grey out its button, so a
 * confirmation without the box ticked opens the dialog again with a reminder.
 * @param show Shows the dialog: Electron's dialog.showMessageBox, or a stand-in in the tests.
 * @returns Whether the user confirmed.
 */
export async function askConfirmation(
  box: ConfirmationBox,
  show: (box: ConfirmationBox) => Promise<DialogAnswer>,
): Promise<boolean> {
  let shown = box;
  for (;;) {
    const answer = await show(shown);
    if (answer.response !== 1) {
      return false;
    }
    if (!box.requiresCheckbox || answer.checkboxChecked) {
      return true;
    }
    shown = { ...box, message: `${box.checkboxReminder ?? ''}\n\n${box.message}`.trim() };
  }
}
