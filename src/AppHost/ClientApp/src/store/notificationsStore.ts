import { defineStore, acceptHMRUpdate } from 'pinia';
import { reactive, computed, toRefs } from 'vue';
import { switchMap, tap } from 'rxjs/operators';
import type { Observable } from 'rxjs';
import { of } from 'rxjs';
import type { NotificationDTO } from '@dto';
import { StoreNames, type ISetupResult } from '@interfaces';
import { notificationApi } from '@api';
import { cloneDeep } from 'lodash-es';

interface INotificationStoreState {
	notifications: NotificationDTO[];
}

export const useNotificationsStore = defineStore(StoreNames.NotificationsStore, () => {
	const defaultState: INotificationStoreState = {
		notifications: [],
	};

	const state = reactive<INotificationStoreState>(cloneDeep(defaultState));

	const telemetry = useTelemetry();
	const actions = {
		setup(): Observable<ISetupResult> {
			return actions.fetchNotifications().pipe(
				switchMap(() =>
					of({
						name: StoreNames.NotificationsStore,
						isSuccess: true,
					}),
				),
			);
		},
		fetchNotifications() {
			return notificationApi.getAllNotificationsEndpoint().pipe(
				tap((result) => {
					if (result.isSuccess) {
						state.notifications = result.value ?? [];
					}
				}),
			);
		},
		setNotification(notification: NotificationDTO) {
			state.notifications.push(notification);
		},
		hideNotification(id: number) {
			telemetry.notificationDismissed(id);
			return notificationApi.setNotificationVisibilityEndpoint({ id, hidden: true }).pipe(
				tap(({ isSuccess }) => {
					if (!isSuccess) return;
					const index = state.notifications.findIndex((notification) => notification.id === id);
					if (index >= 0) {
						const current = state.notifications[index]!;
						state.notifications.splice(index, 1, { ...current, hidden: true });
					}
				}),
			);
		},
		clearAllNotifications() {
			telemetry.notificationsCleared();
			const ids = new Set(state.notifications.map((notification) => notification.id));
			return notificationApi.clearAllNotificationsEndpoint().pipe(
				tap(({ isSuccess }) => {
					if (isSuccess) state.notifications = state.notifications.filter((notification) => !ids.has(notification.id));
				}),
			);
		},
		$reset() {
			Object.assign(state, cloneDeep(defaultState));
		},
	};

	const getters = {
		getNotifications: computed((): NotificationDTO[] => state.notifications),
		getVisibleNotifications: computed((): NotificationDTO[] => state.notifications.filter((x) => !x.hidden)),
	};

	return {
		...toRefs(state),
		...actions,
		...getters,
	};
});

if (import.meta.hot) {
	import.meta.hot.accept(acceptHMRUpdate(useNotificationsStore, import.meta.hot));
}
