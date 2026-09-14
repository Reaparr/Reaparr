import { describe, beforeAll, afterEach, beforeEach, test, expect, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { Subject } from 'rxjs';
import { subscribeSpyTo, baseSetup, getAxiosMock, baseVars } from '@services-test-base';
import { PlexAccountPaths } from '@api/api-paths';
import { StoreNames, type ISetupResult } from '@interfaces';
import { generateFailedResultDTO, generatePlexAccount, generateResultDTO } from '@mock';
import { RefreshDataType } from '@dto';
import { useAccountStore, useSignalrStore } from '@store';

describe('AccountStore.setup()', () => {
	let { mock } = baseVars();

	beforeAll(() => {
		baseSetup();
	});

	beforeEach(() => {
		mock = getAxiosMock();
		setActivePinia(createPinia());
		vi.useFakeTimers();
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	test('Should return success and complete when setup is run', async () => {
		// Arrange
		const accountStore = useAccountStore();
		mock.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint()).reply(200, generateResultDTO([]));
		const setup$ = accountStore.setup();
		const setupResult: ISetupResult = {
			isSuccess: true,
			name: StoreNames.AccountStore,
		};

		// Act
		const result = subscribeSpyTo(setup$);
		await result.onComplete();

		// Assert
		expect(result.getFirstValue()).toEqual(setupResult);
		expect(result.receivedComplete()).toEqual(true);
	});

	test('Should report failure when refreshing accounts fails', async () => {
		// Arrange
		const accountStore = useAccountStore();
		mock.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint()).reply(500, generateFailedResultDTO());
		const setupResult: ISetupResult = {
			isSuccess: false,
			name: StoreNames.AccountStore,
		};

		// Act
		const result = subscribeSpyTo(accountStore.setup());
		await result.onComplete();

		// Assert
		expect(result.getFirstValue()).toEqual(setupResult);
		expect(result.receivedComplete()).toEqual(true);
	});

	test('Should queue account refresh notifications without cancelling an active request', async () => {
		// Arrange
		const notificationSubject = new Subject<RefreshDataType>();
		const signalrStore = useSignalrStore();
		vi.spyOn(signalrStore, 'getRefreshNotification').mockReturnValue(notificationSubject);
		const accountStore = useAccountStore();
		const firstAccounts = [generatePlexAccount({ id: 1, config: { seed: 601 } })];
		const secondAccounts = [generatePlexAccount({ id: 2, config: { seed: 602 } })];
		mock.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint()).replyOnce(200, generateResultDTO([]));
		await subscribeSpyTo(accountStore.setup()).onComplete();
		mock.resetHistory();

		let resolveFirstResponse: (response: [number, unknown]) => void = () => undefined;
		let resolveSecondResponse: (response: [number, unknown]) => void = () => undefined;
		const firstResponse = new Promise<[number, unknown]>((resolve) => {
			resolveFirstResponse = resolve;
		});
		const secondResponse = new Promise<[number, unknown]>((resolve) => {
			resolveSecondResponse = resolve;
		});
		mock.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint())
			.replyOnce(() => firstResponse)
			.onGet(PlexAccountPaths.getAllPlexAccountsEndpoint())
			.reply(() => secondResponse);

		// Act
		notificationSubject.next(RefreshDataType.PlexAccount);
		notificationSubject.next(RefreshDataType.PlexAccount);
		expect(mock.history.get).toHaveLength(1);
		resolveFirstResponse([200, generateResultDTO(firstAccounts)]);
		await vi.advanceTimersByTimeAsync(0);
		expect(accountStore.accounts).toEqual(firstAccounts);
		expect(mock.history.get).toHaveLength(2);
		resolveSecondResponse([200, generateResultDTO(secondAccounts)]);
		await vi.advanceTimersByTimeAsync(0);

		// Assert
		expect(mock.history.get).toHaveLength(2);
		expect(accountStore.accounts).toEqual(secondAccounts);
	});
});
