import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, toRefs } from 'vue';
import type { Observable } from 'rxjs';
import { of } from 'rxjs';
import { StoreNames, type ISetupResult, type IAlert, type IApiError } from '@interfaces';
import { cloneDeep } from 'lodash-es';

interface IAlertStoreState {
	alerts: IAlert[];
}

export const useAlertStore = defineStore(StoreNames.AlertStore, () => {
	const defaultState: IAlertStoreState = {
		alerts: [],
	};
	const state = reactive<IAlertStoreState>(cloneDeep(defaultState));

	const actions = {
		setup(): Observable<ISetupResult> {
			return of({ name: StoreNames.AlertStore, isSuccess: true });
		},
		showAlert(alert: IAlert): void {
			const newAlert = { ...alert, id: Date.now() };
			state.alerts.push(newAlert);
		},
		showApiError(error: IApiError): void {
			const alert = state.alerts.find((item) => item.apiErrors);
			if (!alert?.apiErrors) {
				state.alerts.push({
					id: -1,
					title: '',
					text: '',
					apiErrors: [error],
					hasOmittedApiErrors: false,
				});
				return;
			}

			if (alert.apiErrors.some((item) =>
				item.method === error.method && item.url === error.url
				&& item.statusCode === error.statusCode && item.code === error.code)) {
				return;
			}

			if (alert.apiErrors.length === 10) {
				alert.hasOmittedApiErrors = true;
				return;
			}

			alert.apiErrors.push(error);
		},
		removeAlert(id: number): void {
			state.alerts = state.alerts.filter((x) => x.id !== id);
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};
	return {
		...toRefs(state),
		...actions,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useAlertStore, import.meta.hot));
}
