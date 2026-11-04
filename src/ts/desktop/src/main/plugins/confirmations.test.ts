import { FluentResource } from '@fluent/bundle';
import { describe, expect, it } from 'vitest';

import {
  askConfirmation,
  DIALOG_MESSAGE_FILES,
  enableConfirmation,
  installConfirmation,
  packageFileDialog,
  pickDialogLocale,
  uninstallConfirmation,
  type ConfirmationBox,
  type DialogAnswer,
  type InstallFacts,
} from './confirmations';

const SHA256 = 'b'.repeat(64);

const FACTS: InstallFacts = {
  fileName: 'tool.voyagerplugin',
  sha256: SHA256,
  id: 'tool',
  version: '1.2.0',
  contracts: [
    { contract: 'diagnostic-fact-source', version: 1, role: 'provides', deprecated: false },
    { contract: 'plugin', version: 1, role: 'requires', deprecated: false },
  ],
  hasDotnet: true,
  hasUi: true,
  verification: 'unverified',
  duplicateOfShipped: false,
  kind: 'new',
};

function lines(box: ConfirmationBox): string[] {
  return box.detail.split('\n');
}

describe('installConfirmation', () => {
  it('shows the warning, then the file, the fingerprint, the identifier, the version, the contracts, what it brings and its verification, in that order', () => {
    const shown = lines(installConfirmation(FACTS, 'fr'));
    const expected = [
      'Un plugin .NET s’exécute avec vos droits sur cet ordinateur.',
      'Fichier : tool.voyagerplugin',
      `Empreinte (SHA-256) : ${SHA256}`,
      'Identifiant : tool',
      'Version : 1.2.0',
      'Contrats fournis : diagnostic-fact-source v1',
      'Contrats requis : plugin v1',
      'Apporte : du code .NET et une interface',
      'Vérification : Non vérifié',
    ];

    const positions = expected.map((text) => shown.findIndex((line) => line.startsWith(text)));

    expect(positions).not.toContain(-1);
    expect(positions).toEqual([...positions].sort((first, second) => first - second));
  });

  it('warns that the plugin runs with the rights of the user, can read the trips, and that its author is not verified', () => {
    const detail = installConfirmation(FACTS, 'fr').detail;

    expect(detail).toContain('Un plugin .NET s’exécute avec vos droits sur cet ordinateur.');
    expect(detail).toContain('Un plugin d’interface peut parler au service de Voyager et lire vos voyages.');
    expect(detail).toContain('L’auteur de ce plugin n’est pas vérifié.');
  });

  it('has the warning in English too', () => {
    const detail = installConfirmation(FACTS, 'en').detail;

    expect(detail).toContain('A .NET plugin runs with your rights on this computer.');
    expect(detail).toContain('The author of this plugin is not verified.');
  });

  it('makes "Cancel" the default, and waits for "I understand" to be ticked', () => {
    const box = installConfirmation(FACTS, 'fr');

    expect(box.buttons).toEqual(['Annuler', 'Installer']);
    expect(box.defaultId).toBe(0);
    expect(box.cancelId).toBe(0);
    expect(box.checkboxLabel).toBe('Je comprends');
    expect(box.checkboxChecked).toBe(false);
    expect(box.requiresCheckbox).toBe(true);
  });

  it('says a replacement replaces the installed version, and that nothing proves the author is the same', () => {
    const box = installConfirmation({ ...FACTS, kind: 'upgrade', installedVersion: '1.0.0', version: '2.0.0' }, 'fr');

    expect(box.message).toBe('Remplacer la version 1.0.0 du plugin « tool » par la version 2.0.0 ?');
    expect(box.detail).toContain('Rien ne prouve que l’auteur de la nouvelle version soit celui de l’ancienne.');
    expect(box.buttons[1]).toBe('Remplacer');
  });

  it('asks apart for an older version', () => {
    const box = installConfirmation({ ...FACTS, kind: 'downgrade', installedVersion: '2.0.0', version: '1.0.0' }, 'fr');

    expect(box.message).toBe(
      'Installer une version plus ancienne du plugin « tool » ? La version 1.0.0 remplacera la version 2.0.0.',
    );
  });

  it('offers the same version as a reinstallation', () => {
    const box = installConfirmation({ ...FACTS, kind: 'reinstall', installedVersion: '1.2.0' }, 'en');

    expect(box.message).toBe('Reinstall version 1.2.0 of the plugin “tool”?');
    expect(box.buttons[1]).toBe('Reinstall');
  });

  it('warns of a plugin shipped with Voyager under the same identifier', () => {
    const box = installConfirmation({ ...FACTS, duplicateOfShipped: true }, 'fr');

    expect(box.detail).toContain('Un plugin livré avec Voyager porte déjà cet identifiant');
  });

  it('says which contract is deprecated', () => {
    const box = installConfirmation(
      {
        ...FACTS,
        contracts: [{ contract: 'old-source', version: 1, role: 'provides', deprecated: true }],
      },
      'fr',
    );

    expect(box.detail).toContain('Le contrat « old-source » en version 1 est déprécié');
  });

  it('says what a plugin with only an interface brings', () => {
    expect(installConfirmation({ ...FACTS, hasDotnet: false }, 'fr').detail).toContain('Apporte : une interface');
  });

  it('cleans what comes from the package: no line breaks, no reversed text, at most 100 characters', () => {
    const box = installConfirmation(
      { ...FACTS, fileName: 'evil\n‮gnp.voyagerplugin', id: `x${'y'.repeat(200)}` },
      'fr',
    );

    expect(box.detail).toContain('Fichier : evilgnp.voyagerplugin');
    const identifier = lines(box).find((line) => line.startsWith('Identifiant : ')) ?? '';
    expect(identifier.length).toBeLessThanOrEqual('Identifiant : '.length + 100);
  });

  it('needs no box for a verified package, and still warns for anything else', () => {
    const verified = installConfirmation({ ...FACTS, verification: 'verified' }, 'fr');
    const guaranteed = installConfirmation({ ...FACTS, verification: 'guaranteed' }, 'fr');

    expect(verified.requiresCheckbox).toBe(false);
    expect(verified.checkboxLabel).toBeUndefined();
    expect(verified.detail).toContain('Vérification : Vérifié');
    expect(guaranteed.requiresCheckbox).toBe(true);
  });
});

describe('uninstallConfirmation and enableConfirmation', () => {
  const plugin = { id: 'tool', version: '1.2.0', verification: 'unverified' } as const;

  it('recalls the suspicion before enabling a suspended plugin again, waits for "I understand", and defaults to "Cancel"', () => {
    const box = enableConfirmation(plugin, 'fr', true);

    expect(box.message).toBe('Réactiver le plugin « tool » (version 1.2.0) ?');
    expect(box.detail).toContain('Ce plugin est soupçonné d’avoir fait planter Voyager.');
    expect(box.detail).toContain('C’est un soupçon, pas une certitude');
    expect(box.buttons).toEqual(['Annuler', 'Réactiver']);
    expect(box.defaultId).toBe(0);
    expect(box.requiresCheckbox).toBe(true);
    expect(box.checkboxLabel).toBe('Je comprends');
  });

  it('recalls the suspicion in English too', () => {
    expect(enableConfirmation(plugin, 'en', true).detail).toContain(
      'This plugin is suspected of having crashed Voyager.',
    );
  });

  it('asks to uninstall, says trip data is not touched, and defaults to "Cancel"', () => {
    const box = uninstallConfirmation(plugin, 'fr');

    expect(box.message).toBe('Désinstaller le plugin « tool » (version 1.2.0) ?');
    expect(box.detail).toContain('Vos données de voyage ne sont pas touchées.');
    expect(box.buttons).toEqual(['Annuler', 'Désinstaller']);
    expect(box.defaultId).toBe(0);
  });

  it('asks to enable without the box "I understand"', () => {
    const box = enableConfirmation(plugin, 'en');

    expect(box.message).toBe('Enable the plugin “tool” (version 1.2.0)?');
    expect(box.detail).toContain('Verification: Not verified');
    expect(box.requiresCheckbox).toBe(false);
    expect(box.checkboxLabel).toBeUndefined();
  });

  it('says a shipped plugin is guaranteed by the application, never verified', () => {
    const box = enableConfirmation({ ...plugin, verification: 'guaranteed' }, 'fr');

    expect(box.detail).toContain('Vérification : Garanti par l’application');
  });
});

describe('askConfirmation', () => {
  function answering(...answers: DialogAnswer[]) {
    const shown: ConfirmationBox[] = [];
    return {
      shown,
      show: (box: ConfirmationBox) => {
        shown.push(box);
        return Promise.resolve(answers.shift() ?? { response: 0, checkboxChecked: false });
      },
    };
  }

  it('is a no when the user cancels', async () => {
    const dialog = answering({ response: 0, checkboxChecked: true });

    expect(await askConfirmation(installConfirmation(FACTS, 'fr'), dialog.show)).toBe(false);
  });

  it('is a yes when the user confirms with the box ticked', async () => {
    const dialog = answering({ response: 1, checkboxChecked: true });

    expect(await askConfirmation(installConfirmation(FACTS, 'fr'), dialog.show)).toBe(true);
  });

  it('asks again, with a reminder, when the user confirms without ticking the box', async () => {
    const dialog = answering({ response: 1, checkboxChecked: false }, { response: 1, checkboxChecked: true });

    expect(await askConfirmation(installConfirmation(FACTS, 'fr'), dialog.show)).toBe(true);
    expect(dialog.shown).toHaveLength(2);
    expect(dialog.shown[1]?.message).toContain('Cochez « Je comprends » pour confirmer.');
    expect(dialog.shown[1]?.checkboxChecked).toBe(false);
  });

  it('is a no when the user cancels after the reminder', async () => {
    const dialog = answering({ response: 1, checkboxChecked: false }, { response: 0, checkboxChecked: false });

    expect(await askConfirmation(installConfirmation(FACTS, 'fr'), dialog.show)).toBe(false);
  });

  it('needs no box when the confirmation does not ask for it', async () => {
    const dialog = answering({ response: 1, checkboxChecked: false });

    expect(
      await askConfirmation(
        enableConfirmation({ id: 'tool', version: '1', verification: 'unverified' }, 'fr'),
        dialog.show,
      ),
    ).toBe(true);
  });
});

describe('pickDialogLocale', () => {
  it.each([
    [['en-GB', 'fr-FR'], 'en'],
    [['de-DE', 'fr-CA'], 'fr'],
    [['de-DE'], 'fr'],
    [[], 'fr'],
  ] as const)('picks the language of %j: %s', (preferred, locale) => {
    expect(pickDialogLocale(preferred)).toBe(locale);
  });
});

describe('packageFileDialog', () => {
  it('names the dialog and its filter in both languages', () => {
    expect(packageFileDialog('fr')).toEqual({
      title: 'Ajouter un paquet de plugin',
      filterName: 'Paquets de plugin Voyager',
    });
    expect(packageFileDialog('en').title).toBe('Add a plugin package');
  });
});

describe('message files of the dialogs (ADR-0008)', () => {
  const VARIABLE = /\{\s*\$([A-Za-z][\w-]*)/g;

  function readMessages(source: string): Map<string, string[]> {
    const messages = new Map<string, string[]>();
    for (const block of source.split(/\n(?=[a-z])/)) {
      const id = /^([a-z][\w-]*)\s*=/.exec(block)?.[1];
      if (id !== undefined) {
        messages.set(id, [...block.matchAll(VARIABLE)].map((match) => match[1] ?? '').sort());
      }
    }
    return messages;
  }

  it.each(['fr', 'en'] as const)('parse without errors in %s', (locale) => {
    expect(new FluentResource(DIALOG_MESSAGE_FILES[locale]).body.length).toBeGreaterThan(0);
  });

  it('define the same messages, with the same variables, in French and English', () => {
    const french = readMessages(DIALOG_MESSAGE_FILES.fr);
    const english = readMessages(DIALOG_MESSAGE_FILES.en);

    expect([...english.keys()].sort()).toEqual([...french.keys()].sort());
    for (const [id, variables] of french) {
      expect(english.get(id), id).toEqual(variables);
    }
  });
});
