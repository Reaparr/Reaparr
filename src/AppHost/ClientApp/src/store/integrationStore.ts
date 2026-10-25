import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { computed, reactive, toRefs, watch, shallowRef } from 'vue';
import { get, set } from '@vueuse/core';
import type { Observable } from 'rxjs';
import { of, EMPTY } from 'rxjs';
import { catchError, finalize, filter, map, switchMap, tap } from 'rxjs/operators';
import { cloneDeep, isEqual } from 'lodash-es';
import { integrationApi } from '@api';
import {
	IntegrationProvisioningState,
	type IntegrationSummary,
	IntegrationType,
	TestConnectionStatus,
	type RadarrIntegrationDTO,
	type SonarrIntegrationDTO,
	type TestConnectionToRadarrEndpointResponse,
	type TestConnectionToSonarrEndpointResponse,
} from '@dto';
import { type ISetupResult, type ResultDTO, StoreNames } from '@interfaces';

interface IIntegrationDraft {
	type: IntegrationType;
	name: string;
	url: string;
	apiKey: string;
	category: string;
	downloadFolderId: number;
}

type IntegrationDetail = (RadarrIntegrationDTO | SonarrIntegrationDTO) & { type: IntegrationType };
type TestConnectionResult = TestConnectionToRadarrEndpointResponse | TestConnectionToSonarrEndpointResponse;

interface IIntegrationStoreState {
	items: IntegrationSummary[];
	detail: IntegrationDetail | null;
	draft: IIntegrationDraft;
	isTesting: boolean;
	isSaving: boolean;
	isSettingUp: boolean;
	isDeleting: boolean;
	testResult: TestConnectionResult | null;
	error: unknown;
	saveError: unknown;
	setupError: unknown;
	requiresSetupPrompt: boolean;
}

function emptyDraft(type = IntegrationType.Sonarr): IIntegrationDraft {
	return {
		type,
		name: '',
		url: '',
		apiKey: '',
		category: '',
		downloadFolderId: 1,
	};
}

export const useIntegrationStore = defineStore(StoreNames.IntegrationStore, () => {
	const defaultState: IIntegrationStoreState = {
		items: [],
		detail: null,
		draft: emptyDraft(),
		isTesting: false,
		isSaving: false,
		isSettingUp: false,
		isDeleting: false,
		testResult: null,
		error: null,
		saveError: null,
		setupError: null,
		requiresSetupPrompt: false,
	};

	const state = reactive<IIntegrationStoreState>(cloneDeep(defaultState));
	const editTarget = shallowRef<Pick<IntegrationSummary, 'id' | 'type'> | null>(null);
	let dialogSession = 0;
	const requestVersions = { load: 0, mutation: 0, test: 0 };
	let observedDraftUrl = state.draft.url.trim().replace(/\/+$/, '');
	let observedDraftApiKey = state.draft.apiKey.trim();
	watch(
		() => [state.draft.url, state.draft.apiKey] as const,
		([url, apiKey]) => {
			const normalizedUrl = url.trim().replace(/\/+$/, '');
			const normalizedApiKey = apiKey.trim();
			const identityChanged = normalizedUrl !== observedDraftUrl
				|| normalizedApiKey !== observedDraftApiKey;
			observedDraftUrl = normalizedUrl;
			observedDraftApiKey = normalizedApiKey;
			if (identityChanged && !state.isTesting) state.testResult = null;
		},
		{ flush: 'sync' },
	);

	const actions = {
		setup(): Observable<ISetupResult> {
			return actions.refresh().pipe(
				map(() => ({ name: StoreNames.IntegrationStore, isSuccess: true })),
				catchError((error) => {
					Log.error('Failed to setup integration store', error);
					return of({ name: StoreNames.IntegrationStore, isSuccess: false });
				}),
			);
		},
		refresh() {
			const testResult = state.testResult;
			return integrationApi.getIntegrationsEndpoint().pipe(
				tap((result) => {
					if (result.isSuccess) {
						state.items = result.value ?? [];
						if (testResult && state.testResult === testResult)
							updateSummaryConnectionStatus(testResult);
					}
				}),
			);
		},
		openAdd(type = IntegrationType.Sonarr): void {
			resetOperationState();
			state.detail = null;
			state.draft = { ...emptyDraft(type), category: getUniqueCategory(type) };
		},
		openEdit(item: IntegrationSummary) {
			resetOperationState();
			set(editTarget, { id: item.id, type: item.type });
			state.detail = null;
			state.draft = emptyDraft(item.type);
			const ownsRequest = beginRequest('load');
			let request: Observable<ResultDTO<RadarrIntegrationDTO | SonarrIntegrationDTO>>;
			switch (item.type) {
				case IntegrationType.Radarr:
					request = integrationApi.getRadarrIntegrationEndpoint(item.id);
					break;
				case IntegrationType.Sonarr:
					request = integrationApi.getSonarrIntegrationEndpoint(item.id);
					break;
				default:
					throw new Error(`Unsupported integration type: ${String(item.type)}`);
			}
			return request.pipe(
				filter(ownsRequest),
				tap((result) => {
					if (result.isSuccess && result.value) setDetail(item.type, result.value);
					else state.error = result;
				}),
				catchError((error) => ownsRequest() ? handleError(error) : EMPTY),
			);
		},
		test() {
			const testedDraft = { ...state.draft };
			const testedIntegrationId = state.detail?.id;
			const ownsRequest = beginRequest('test');
			const isCurrent = () => ownsRequest() && isTestTargetCurrent(testedDraft, testedIntegrationId);
			const query = {
				apiKey: testedDraft.apiKey,
				url: testedDraft.url,
				...(testedIntegrationId ? { integrationId: testedIntegrationId } : {}),
			};
			let request: Observable<ResultDTO<TestConnectionResult>>;
			switch (testedDraft.type) {
				case IntegrationType.Radarr:
					request = integrationApi.testConnectionToRadarrEndpoint(query);
					break;
				case IntegrationType.Sonarr:
					request = integrationApi.testConnectionToSonarrEndpoint(query);
					break;
				default:
					throw new Error(`Unsupported integration type: ${String(testedDraft.type)}`);
			}
			state.isTesting = true;
			state.testResult = null;
			state.error = null;
			state.saveError = null;
			return request.pipe(
				filter(isCurrent),
				tap((result) => {
					if (!result.value) {
						state.error = result;
						return;
					}
					state.testResult = result.value;
					updateSummaryConnectionStatus(result.value);
				}),
				catchError((error) => isCurrent() ? handleError(error) : EMPTY),
				finalize(() => {
					if (ownsRequest()) state.isTesting = false;
				}),
			);
		},
		save() {
			if (!isEditTargetReady() || !isHttpUrl(state.draft.url)) return EMPTY;
			const ownsRequest = beginRequest('mutation');
			let expectedTarget = captureDraftTarget();
			const isCurrent = () => ownsRequest() && isEqual(expectedTarget, captureDraftTarget());
			const type = state.draft.type;
			const request = createSaveRequest(type);
			state.isSettingUp = false;
			state.isSaving = true;
			state.error = null;
			state.saveError = null;
			return request.pipe(
				tap((result) => {
					if (!isCurrent()) return;
					handleSaveResult(type, result);
					expectedTarget = captureDraftTarget();
				}),
				switchMap((result) => result.isSuccess ? actions.refresh().pipe(map(() => result)) : of(result)),
				filter(isCurrent),
				catchError((error) => isCurrent() ? handleSaveError(error) : EMPTY),
				finalize(() => {
					if (ownsRequest()) state.isSaving = false;
				}),
			);
		},
		setupIntegration() {
			const detail = state.detail;
			if (!detail) return of(null);
			const ownsRequest = beginRequest('mutation');
			let expectedTarget = captureDraftTarget();
			const isCurrent = () => ownsRequest() && isEqual(expectedTarget, captureDraftTarget());
			const request = createSetupRequest(detail);
			state.isSaving = false;
			state.isSettingUp = true;
			state.error = null;
			state.setupError = null;
			state.testResult = null;
			return request.pipe(
				tap((result) => {
					if (!isCurrent()) return;
					handleSetupResult(detail.type, result);
					expectedTarget = captureDraftTarget();
				}),
				switchMap((result) => result.isSuccess ? actions.refresh().pipe(map(() => result)) : of(result)),
				filter(isCurrent),
				catchError((error) => isCurrent() ? handleSetupError(error) : EMPTY),
				finalize(() => {
					if (ownsRequest()) state.isSettingUp = false;
				}),
			);
		},
		delete() {
			const detail = state.detail;
			if (!detail) return of(null);
			let request: Observable<unknown>;
			switch (detail.type) {
				case IntegrationType.Radarr:
					request = integrationApi.deleteRadarrIntegrationEndpoint(detail.id, { force: true });
					break;
				case IntegrationType.Sonarr:
					request = integrationApi.deleteSonarrIntegrationEndpoint(detail.id, { force: true });
					break;
				default:
					throw new Error(`Unsupported integration type: ${String(detail.type)}`);
			}
			state.isDeleting = true;
			state.error = null;
			return (request as Observable<ResultDTO>).pipe(
				tap((result) => {
					if (!result.isSuccess) {
						state.error = result;
						return;
					}
					state.detail = null;
					state.requiresSetupPrompt = false;
				}),
				catchError(handleError),
				finalize(() => (state.isDeleting = false)),
			);
		},
		close(): void {
			resetOperationState();
			state.detail = null;
			state.draft = emptyDraft();
		},
		$reset(): void {
			resetOperationState();
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	function beginRequest(operation: keyof typeof requestVersions): () => boolean {
		const session = dialogSession;
		const version = ++requestVersions[operation];
		return () => session === dialogSession && version === requestVersions[operation];
	}

	function captureDraftTarget() {
		return { ...state.draft, integrationId: state.detail?.id };
	}

	function isEditTargetReady(): boolean {
		const target = get(editTarget);
		return target === null || (state.detail?.id === target.id && state.detail.type === target.type);
	}

	function getUniqueCategory(type: IntegrationType): string {
		let baseCategory: string;
		switch (type) {
			case IntegrationType.Sonarr:
				baseCategory = 'reaparr-sonarr';
				break;
			case IntegrationType.Radarr:
				baseCategory = 'reaparr-radarr';
				break;
		}
		const categories = new Set(
			state.items
				.filter((item) => item.type === type)
				.map((item) => item.category),
		);
		if (!categories.has(baseCategory)) return baseCategory;

		let suffix = 2;
		while (categories.has(`${baseCategory}-${suffix}`)) suffix++;
		return `${baseCategory}-${suffix}`;
	}

	function resetOperationState(): void {
		dialogSession++;
		set(editTarget, null);
		state.isTesting = false;
		state.isSaving = false;
		state.isSettingUp = false;
		state.error = null;
		state.saveError = null;
		state.setupError = null;
		state.testResult = null;
		state.requiresSetupPrompt = false;
	}

	function isTestTargetCurrent(testedDraft: IIntegrationDraft, testedIntegrationId?: string): boolean {
		return state.detail?.id === testedIntegrationId
			&& state.draft.type === testedDraft.type
			&& state.draft.url === testedDraft.url
			&& state.draft.apiKey === testedDraft.apiKey;
	}

	function updateSummaryConnectionStatus(result: TestConnectionResult): void {
		if (!state.detail) return;

		const index = state.items.findIndex((item) => item.id === state.detail!.id);
		state.detail = {
			...state.detail,
			lastConnectionTestStatus: result.result,
			lastConnectionTestHttpStatusCode: result.httpStatusCode,
			lastConnectionTestErrorMessage: result.errorMessage,
			lastConnectionTestedAt: result.testedAt,
		};
		if (index === -1) return;

		const integration = state.items[index];
		if (!integration) return;
		state.items.splice(index, 1, {
			...integration,
			lastConnectionTestStatus: result.result,
			lastConnectionTestHttpStatusCode: result.httpStatusCode,
			lastConnectionTestErrorMessage: result.errorMessage,
			lastConnectionTestedAt: result.testedAt,
		});
	}

	function setDetail(type: IntegrationType, detail: RadarrIntegrationDTO | SonarrIntegrationDTO): void {
		state.detail = { ...detail, type };
		state.draft = {
			type,
			name: detail.name,
			url: detail.url,
			apiKey: detail.apiKey,
			category: detail.category,
			downloadFolderId: detail.downloadFolderId,
		};
	}

	function createSaveRequest(type: IntegrationType): Observable<ResultDTO<RadarrIntegrationDTO | SonarrIntegrationDTO>> {
		const request = toRequest();
		switch (type) {
			case IntegrationType.Radarr:
				return state.detail
					? integrationApi.updateRadarrIntegrationEndpoint(state.detail.id, request)
					: integrationApi.createRadarrIntegrationEndpoint(request);
			case IntegrationType.Sonarr:
				return state.detail
					? integrationApi.updateSonarrIntegrationEndpoint(state.detail.id, request)
					: integrationApi.createSonarrIntegrationEndpoint(request);
			default:
				throw new Error(`Unsupported integration type: ${String(type)}`);
		}
	}

	function createSetupRequest(detail: IntegrationDetail): Observable<ResultDTO<RadarrIntegrationDTO | SonarrIntegrationDTO>> {
		if (!isEditTargetReady()) return EMPTY;
		switch (detail.type) {
			case IntegrationType.Radarr:
				return integrationApi.setupRadarrIntegrationEndpoint(detail.id);
			case IntegrationType.Sonarr:
				return integrationApi.setupSonarrIntegrationEndpoint(detail.id);
			default:
				throw new Error(`Unsupported integration type: ${String(detail.type)}`);
		}
	}

	function toRequest() {
		return {
			name: state.draft.name,
			url: state.draft.url,
			apiKey: state.draft.apiKey,
			category: state.draft.category,
			downloadFolderId: state.draft.downloadFolderId,
		};
	}

	function handleSaveResult(type: IntegrationType, result: {
		isSuccess: boolean;
		value?: RadarrIntegrationDTO | SonarrIntegrationDTO | null;
	}): void {
		if (result.isSuccess && result.value) {
			setDetail(type, result.value);
			state.requiresSetupPrompt = result.value.provisioningState !== IntegrationProvisioningState.Configured;
		} else {
			state.error = result;
			state.saveError = result;
		}
	}

	function handleSetupResult(type: IntegrationType, result: ResultDTO<RadarrIntegrationDTO | SonarrIntegrationDTO>): void {
		if (result.isSuccess && result.value) {
			setDetail(type, result.value);
			state.requiresSetupPrompt = false;
		} else state.setupError = result;
	}

	function handleSetupError(error: unknown) {
		state.setupError = error;
		Log.error('Integration setup request failed', error);
		return of(null);
	}

	function handleError(error: unknown) {
		state.error = error;
		Log.error('Integration request failed', error);
		return of(null);
	}

	function handleSaveError(error: unknown) {
		state.error = error;
		state.saveError = error;
		Log.error('Integration save request failed', error);
		return of(null);
	}

	function isCategoryUnique(category: string): boolean {
		const normalizedCategory = category.trim();
		if (!normalizedCategory) return true;
		return !state.items.some((item) =>
			item.type === state.draft.type
			&& item.id !== state.detail?.id
			&& item.category === normalizedCategory,
		);
	}

	function isUrlUnique(url: string): boolean {
		const normalizedUrl = url.trim().replace(/\/+$/, '');
		if (!normalizedUrl) return true;
		return !state.items.some((item) =>
			item.type === state.draft.type
			&& item.id !== state.detail?.id
			&& item.baseUrl === normalizedUrl,
		);
	}

	function isDraftDirty(): boolean {
		return !state.detail
			|| state.draft.name !== state.detail.name
			|| state.draft.url !== state.detail.url
			|| state.draft.apiKey !== state.detail.apiKey
			|| state.draft.category !== state.detail.category
			|| state.draft.downloadFolderId !== state.detail.downloadFolderId;
	}

	function isHttpUrl(value: string): boolean {
		try {
			const { protocol } = new URL(value.trim());
			return protocol === 'http:' || protocol === 'https:';
		} catch {
			return false;
		}
	}

	function isDraftValid(): boolean {
		if (!isEditTargetReady()) return false;
		return Boolean(
			state.draft.name.trim()
			&& isHttpUrl(state.draft.url)
			&& state.draft.apiKey.trim()
			&& state.draft.category.trim()
			&& state.draft.downloadFolderId > 0
			&& isCategoryUnique(state.draft.category)
			&& isUrlUnique(state.draft.url),
		);
	}

	const getters = {
		isHttpUrl,
		isCategoryUnique,
		isUrlUnique,
		isDraftDirty: computed(isDraftDirty),
		shouldSaveBeforeSetup: computed(isDraftDirty),
		isSetupDisabled: computed(() => state.isTesting
			|| state.isSaving
			|| state.isSettingUp
			|| (isDraftDirty() && (
				!isDraftValid()
				|| state.testResult?.result !== TestConnectionStatus.Success
			))),
		isDraftValid: computed(isDraftValid),
	};

	return { ...toRefs(state), ...actions, ...getters };
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useIntegrationStore, import.meta.hot));
}
