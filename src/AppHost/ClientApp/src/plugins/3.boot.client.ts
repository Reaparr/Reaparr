import Log from 'consola';
import Axios from 'axios';
import { useAlertStore, useGlobalStore, useLocalizationStore } from '@store';
import { showErrorNotification } from '@composables/notification';
import { canSendDesktopMessage, sendDesktopMessage } from '@composables/desktop-message-hub';
import { DesktopMessageType } from '@dto';
import type IAppConfig from '@class/IAppConfig';
import type { Router } from 'vue-router';
import type { I18nObjectType } from '@interfaces';
import { defineNuxtPlugin } from '#app';

export default defineNuxtPlugin((nuxtApp) => {
	const publicEnv = useRuntimeConfig().public;

	nuxtApp.hook('app:created', () => {
		Log.level = 4;
		// Log.level = config.public.isProduction ? LogLevel.Debug : LogLevel.Debug;

		let baseUrl = `http://localhost:${publicEnv.apiPort}`;

		if (publicEnv.platform === 'docker') {
			const currentLocation = window.location;
			baseUrl = `${currentLocation.protocol}//${currentLocation.hostname}:${currentLocation.port}`;
		}

		const appConfig: IAppConfig = {
			nodeEnv: publicEnv.nodeEnv,
			isProduction: publicEnv.nodeEnv === 'production',
			platform: publicEnv.platform,
			version: publicEnv.version,
			baseUrl,
		};
		useLocalizationStore().setI18nObject(nuxtApp.$i18n as I18nObjectType);
		setupAxios(appConfig, nuxtApp.$router as Router, (nuxtApp.$i18n as I18nObjectType).t);
		useGlobalStore()
			.setupServices({ config: appConfig })
			.subscribe(() => {
				if (appConfig.platform === 'desktop' && canSendDesktopMessage()) {
					sendDesktopMessage({
						type: DesktopMessageType.DesktopReady,
						value: 'ready',
					});
				}
			});
	});
});

export function setupAxios(appConfig: IAppConfig, router: Router, t: (key: string) => string) {
	const backendUrl = new URL(appConfig.baseUrl);
	Axios.defaults.baseURL = appConfig.baseUrl;
	Axios.defaults.withCredentials = true;

	// Source: https://github.com/axios/axios/issues/41#issuecomment-484546457
	// Now error resolves in catch block rather than then block.
	//	Axios.defaults.validateStatus = () => true;

	// Source: https://github.com/axios/axios/issues/41#issuecomment-386762576
	Axios.interceptors.response.use(
		(config) => {
			useGlobalStore().setAppVersion(config.headers['x-reaparr-version']);
			useGlobalStore().setAppPlatform(config.headers['x-reaparr-platform']);
			return config;
		},
		(error) => {
			const status = error.response?.status;

			if (error.response?.headers) {
				useGlobalStore().setAppVersion(error.response.headers['x-reaparr-version']);
				useGlobalStore().setAppPlatform(error.response.headers['x-reaparr-platform']);
			}

			// Redirect to log-in on 401 Unauthorized
			if (status === 401) {
				router.push('/login');
			}

			if (Axios.isAxiosError(error) && error.config && !Axios.isCancel(error)) {
				const isHttpError = status >= 400 && status <= 599 && status !== 401;
				const isConnectivityError = !error.response && (
					error.request || error.code === 'ERR_NETWORK' || error.code === 'ECONNABORTED'
					|| error.code === 'ETIMEDOUT' || error.message === 'Network Error'
				);

				if (isHttpError || isConnectivityError) {
					let url: URL;
					try {
						url = new URL(Axios.getUri(error.config), backendUrl);
					} catch {
						return Promise.reject(error);
					}

					if (url.origin === backendUrl.origin && /^\/api(?:\/|$)/.test(url.pathname)) {
						if (isConnectivityError) {
							showErrorNotification(t('components.alert-dialog.connection-failed'));
						} else {
							const data = error.response?.data;
							const backendMessage = Array.isArray(data?.errors)
								? data.errors.find((item: unknown) =>
									typeof item === 'object' && item !== null
									&& 'message' in item && typeof item.message === 'string')?.message
								: undefined;
							useAlertStore().showApiError({
								method: (error.config.method ?? 'GET').toUpperCase(),
								url: url.href,
								statusCode: status,
								code: error.code,
								message: backendMessage || t('components.alert-dialog.request-failed'),
							});
						}
					}
				}
			}

			// Reject the promise to ensure the calling code can still handle the error
			return Promise.reject(error);
		},
	);
}
