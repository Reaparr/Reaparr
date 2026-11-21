import Log from 'consola';
import { acceptHMRUpdate, defineStore } from 'pinia';
import { reactive, computed, toRefs, unref, markRaw } from 'vue';
import type { Observable } from 'rxjs';
import { of } from 'rxjs';
import { get } from '@vueuse/core';
import { StoreNames, type ISetupResult, type I18nObjectType, type ILocaleConfig } from '@interfaces';
import { useSettingsStore } from '@store';
import { cloneDeep } from 'lodash-es';
import type { LocaleObject } from '@nuxtjs/i18n';
import type { Locale } from 'vue-i18n';

interface ILocalizationStoreState {
	i18nRef: I18nObjectType;
}

export const useLocalizationStore = defineStore(StoreNames.LocalizationStore, () => {
	// State
	const defaultState: ILocalizationStoreState = {
		i18nRef: {} as I18nObjectType,
	};

	const state = reactive<ILocalizationStoreState>(cloneDeep(defaultState));
	const emptyLocale: ILocaleConfig = {
		text: '',
		code: '' as Locale,
		iso: '',
		bcp47Code: 'en-US',
		img: '',
	};

	function isI18nReady(i18n?: unknown): i18n is I18nObjectType {
		const locales = unref((i18n as { locales?: unknown })?.locales);
		return !!i18n
			&& Array.isArray(locales)
			&& typeof (i18n as { setLocale?: unknown }).setLocale === 'function';
	}

	// Actions
	const actions = {
		setup(): Observable<ISetupResult> {
			return of({ name: StoreNames.LocalizationStore, isSuccess: true });
		},
		setI18nObject(i18n?: unknown) {
			if (!isI18nReady(i18n)) {
				Log.error('i18n object is not defined');
				return;
			}

			// @ts-expect-error Vue's reactive state type unwraps refs, but this external runtime object must remain raw.
			state.i18nRef = markRaw(i18n);
			actions.changeLanguageLocale(get(i18n.locale));
		},
		changeLanguageLocale(isoCode: Locale) {
			if (!isI18nReady(state.i18nRef)) {
				Log.error('i18n object is not defined');
				return;
			}
			Log.info('Localization Options:', isoCode);
			state.i18nRef.setLocale(isoCode).then(() => {
				useSettingsStore().languageSettings.language = isoCode;
				Log.info('Localization has been set to:', isoCode);
			}).catch((error) => Log.error('Failed to change localization to:', isoCode, error));
		},
		toILocalConfig(locale?: LocaleObject | null): ILocaleConfig {
			if (!locale?.code || !locale?.name) {
				return emptyLocale;
			}
			return {
				text: locale.name,
				code: locale.code,
				iso: locale.code,
				bcp47Code: locale.code.slice(0, 2),
				img: `/img/flags/${locale.code}.svg`,
			};
		},
		$reset: () => {
		},
	};

	// Getters
	const getters = {
		getLanguageLocale: computed((): ILocaleConfig => {
			if (!isI18nReady(state.i18nRef)) {
				return emptyLocale;
			}

			const locales = unref(state.i18nRef.locales);
			const locale = locales.find((locale) => locale.code === unref(state.i18nRef.locale));
			return actions.toILocalConfig(locale);
		}),
		getLanguageLocaleOptions: computed((): ILocaleConfig[] => {
			if (!isI18nReady(state.i18nRef)) {
				return [];
			}

			return unref(state.i18nRef.locales).map((x) => (actions.toILocalConfig(x)));
		}),
	};

	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useLocalizationStore, import.meta.hot));
}
