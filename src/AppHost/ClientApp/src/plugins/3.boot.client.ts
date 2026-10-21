import Log from 'consola';
import Axios, { type AxiosError, type AxiosRequestConfig } from 'axios';
import { useAlertStore, useAuthenticationStore, useGlobalStore, useLocalizationStore } from '@store';
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

function backendApiUrl(config: AxiosRequestConfig | undefined, backendUrl: URL): URL | null {
	if (!config) return null;
	try {
		const url = new URL(Axios.getUri(config), backendUrl);
		return url.origin === backendUrl.origin && /^\/api(?:\/|$)/.test(url.pathname) ? url : null;
	} catch {
		return null;
	}
}

function createUnauthorizedSessionHandler(backendUrl: URL, router: Router): (config?: AxiosRequestConfig) => Promise<void> {
	let operation: Promise<void> | null = null;
	return async (config) => {
		if (!backendApiUrl(config, backendUrl)) return;
		if (!operation) {
			const reset = useAuthenticationStore().isLoggedIn ? useGlobalStore().resetSession() : Promise.resolve();
			operation = reset
				.then(() => router.push('/login'))
				.then(() => undefined)
				.finally(() => {
					operation = null;
				});
		}
		await operation;
	};
}

function isConnectivityFailure(error: AxiosError): boolean {
	if (error.response) return false;
	if (error.request) return true;
	switch (error.code) {
		case 'ERR_NETWORK':
		case 'ECONNABORTED':
		case 'ETIMEDOUT':
			return true;
		default:
			return error.message === 'Network Error';
	}
}

export function setupAxios(appConfig: IAppConfig, router: Router, t: (key: string) => string) {
	const backendUrl = new URL(appConfig.baseUrl);
	Axios.defaults.baseURL = appConfig.baseUrl;
	Axios.defaults.withCredentials = true;
	const endUnauthorizedSession = createUnauthorizedSessionHandler(backendUrl, router);

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
		async (error) => {
			if (Axios.isCancel(error) || error.config?.signal?.aborted) return Promise.reject(error);
			const status = error.response?.status;

			if (error.response?.headers) {
				useGlobalStore().setAppVersion(error.response.headers['x-reaparr-version']);
				useGlobalStore().setAppPlatform(error.response.headers['x-reaparr-platform']);
			}

			if (status === 401) {
				await endUnauthorizedSession(error.config);
			}

			if (Axios.isAxiosError(error) && error.config && !Axios.isCancel(error) && !error.config.handleErrorsLocally) {
				const isHttpError = status >= 400 && status <= 599 && status !== 401;
				const isConnectivityError = isConnectivityFailure(error);

				if (isHttpError || isConnectivityError) {
					const url = backendApiUrl(error.config, backendUrl);
					if (url) {
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
