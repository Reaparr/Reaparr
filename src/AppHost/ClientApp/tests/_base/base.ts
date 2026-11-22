import type { Context } from 'vm';
import Log from 'consola';
import MockAdapter from 'axios-mock-adapter';
import axios from 'axios';
import type { MockConfig } from '@mock';
import type IAppConfig from '@class/IAppConfig';

export * from '@hirez_io/observer-spy';

export function baseVars(): { ctx: Context; mock: MockAdapter; config: Partial<MockConfig>; appConfig: IAppConfig } {
	let ctx!: Context;
	let mock!: MockAdapter;
	return {
		ctx,
		mock,
		config: {},
		appConfig: {} as IAppConfig,
	};
}

export function baseSetup(): { ctx: Context; appConfig: IAppConfig } {
	const ctx: Context = {
		$config: {
			nodeEnv: 'TESTING',
			version: '1.0',
		},
	} as Context;

	const appConfig: IAppConfig = {
		baseUrl: 'http://localhost:3030/',
		nodeEnv: 'TESTING',
		isProduction: false,
		platform: 'docker',
		version: '1.0.0-test',
	};
	process.env.NODE_ENV = 'dev';
	import.meta.client = true;

	// Minimum LogLevel displayed
	Log.level = 2;
	return {
		ctx,
		appConfig,
	};
}

export function getAxiosMock() {
	// eslint-disable-next-line @typescript-eslint/ban-ts-comment
	// @ts-ignore - https://github.com/ctimmerm/axios-mock-adapter/issues/400
	const mock = new MockAdapter(axios, { onNoMatch: 'throwException' });

	// Default mocks to avoid noisy missing-mock errors in setup flows
	mock.onGet('/api/Authentication/status').reply(200, {
		isSuccess: true,
		errors: [],
		successes: [],
		statusCode: 200,
		value: {
			claims: [],
			isLoggedIn: true,
			userName: 'test-user',
		},
	});
	mock.onGet('/api/BackgroundJobs').reply(200, {
		isSuccess: true,
		errors: [],
		successes: [],
		statusCode: 200,
		value: [],
	});
	mock.onGet('/api/PlexLibrary/sync-status').reply(200, {
		isSuccess: true,
		errors: [],
		successes: [],
		statusCode: 200,
		value: [],
	});

	return mock;
}
