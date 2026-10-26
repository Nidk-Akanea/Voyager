import { dirname } from 'node:path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { launchLocalService, type LaunchOptions } from './localService';

// Stand-ins for the .NET local service, run by the Node.js that runs the tests.
const ANNOUNCES_AND_WAITS = `
  process.stdout.write('info: starting\\nVOYAGER_LISTENING http://127.0.0.1:4321\\n');
  process.stdin.resume();
  process.stdin.on('end', () => process.exit(0));
`;
const FAILS_AT_STARTUP = 'process.exit(3);';
const NEVER_ANNOUNCES = 'setInterval(() => undefined, 1000);';

/** A stand-in that announces the value of a variable instead of its address, then waits. */
function reportVariable(name: string, options: Omit<LaunchOptions, 'executable' | 'args'>) {
  const script = `console.log('VOYAGER_LISTENING ' + process.env[${JSON.stringify(name)}]); process.stdin.resume();`;
  return launchLocalService({ executable: process.execPath, args: ['-e', script], ...options });
}

function fakeService(script: string, startupTimeoutMs?: number) {
  return launchLocalService({ executable: process.execPath, args: ['-e', script], startupTimeoutMs });
}

describe('launchLocalService', () => {
  it('connects to the address the service announces, with a fresh token', async () => {
    const service = fakeService(ANNOUNCES_AND_WAITS);

    const connection = await service.connection;

    expect(connection.url).toBe('http://127.0.0.1:4321');
    expect(connection.token).toMatch(/^[\w-]{43}$/);
    await service.stop();
  });

  it('gives each launch its own token', async () => {
    const first = fakeService(ANNOUNCES_AND_WAITS);
    const second = fakeService(ANNOUNCES_AND_WAITS);

    const [firstConnection, secondConnection] = await Promise.all([first.connection, second.connection]);

    expect(firstConnection.token).not.toBe(secondConnection.token);
    await Promise.all([first.stop(), second.stop()]);
  });

  it('passes the requested environment to the service', async () => {
    const service = reportVariable('DOTNET_ENVIRONMENT', { environment: { DOTNET_ENVIRONMENT: 'Production' } });

    await expect(service.connection).resolves.toMatchObject({ url: 'Production' });
    await service.stop();
  });

  it('starts the service in the folder of its executable, where its settings files are', async () => {
    const service = launchLocalService({
      executable: process.execPath,
      args: ['-e', "console.log('VOYAGER_LISTENING ' + process.cwd()); process.stdin.resume();"],
    });

    await expect(service.connection).resolves.toMatchObject({ url: dirname(process.execPath) });
    await service.stop();
  });

  it('never lets the environment replace its address or token', async () => {
    const service = reportVariable('ASPNETCORE_URLS', { environment: { ASPNETCORE_URLS: 'http://0.0.0.0:5000' } });

    await expect(service.connection).resolves.toMatchObject({ url: 'http://127.0.0.1:0' });
    await service.stop();
  });

  describe('when isolated', () => {
    const MACHINE_SETTING = 'Voyager__DatabasePath';

    beforeEach(() => {
      process.env[MACHINE_SETTING] = 'elsewhere.db';
    });

    afterEach(() => {
      Reflect.deleteProperty(process.env, MACHINE_SETTING);
    });

    it('leaves out the settings of the machine', async () => {
      const service = reportVariable(MACHINE_SETTING, { isolated: true });

      await expect(service.connection).resolves.toMatchObject({ url: 'undefined' });
      await service.stop();
    });

    it('keeps the system locations', async () => {
      const service = reportVariable('PATH', { isolated: true });

      await expect(service.connection).resolves.not.toMatchObject({ url: 'undefined' });
      await service.stop();
    });

    it('still inherits everything when not isolated', async () => {
      const service = reportVariable(MACHINE_SETTING, {});

      await expect(service.connection).resolves.toMatchObject({ url: process.env[MACHINE_SETTING] });
      await service.stop();
    });
  });

  it('stops the service by closing its input', async () => {
    const service = fakeService(ANNOUNCES_AND_WAITS);
    await service.connection;

    await expect(service.stop()).resolves.toBeUndefined();
  });

  it('fails when the service stops before announcing its address', async () => {
    const service = fakeService(FAILS_AT_STARTUP);

    await expect(service.connection).rejects.toThrow('exit code 3');
  });

  it('fails when the executable does not exist', async () => {
    const service = launchLocalService({ executable: 'voyager-missing-local-service' });

    await expect(service.connection).rejects.toThrow('ENOENT');
    await service.stop();
  });

  it('gives up, and stops the service, when it never announces its address', async () => {
    const service = fakeService(NEVER_ANNOUNCES, 300);

    await expect(service.connection).rejects.toThrow('did not start');
    await expect(service.stop()).resolves.toBeUndefined();
  });
});
