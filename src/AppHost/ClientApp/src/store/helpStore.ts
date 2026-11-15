import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import type { Observable } from 'rxjs';
import { of, Subject } from 'rxjs';
import { StoreNames, type IHelp, type ISetupResult } from '@interfaces';
import { cloneDeep } from 'lodash-es';

interface IHelpStoreState {
	helpIdDialog: IHelp;
	helpDialogObservable: Subject<IHelp>;
}

export const useHelpStore = defineStore(StoreNames.HelpStore, () => {
	const defaultState: IHelpStoreState = {
		helpIdDialog: { label: '', title: '', text: '' },
		helpDialogObservable: new Subject<IHelp>(),
	};

	const state = reactive<IHelpStoreState>(cloneDeep(defaultState));

	const telemetry = useTelemetry();
	const actions = {
		setup(): Observable<ISetupResult> {
			return of({ name: StoreNames.HelpStore, isSuccess: true });
		},
		openHelpDialog(help: IHelp): void {
			if (!help) {
				return;
			}
			telemetry.helpOpened();
			state.helpIdDialog = help;
			state.helpDialogObservable.next(help);
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};
	const getters = {
		getHelpDialog: computed((): Observable<IHelp> => state.helpDialogObservable),
	};
	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useHelpStore, import.meta.hot));
}
