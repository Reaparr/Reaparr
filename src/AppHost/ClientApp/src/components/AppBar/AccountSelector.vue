<template>
	<q-btn
		icon="mdi-account"
		aria-label="Account selector"
		flat
		rounded
		data-cy="account-selector-btn"
		style="padding: 0.5rem">
		<q-menu>
			<q-list class="account-selector-list">
				<template v-if="accountsDisplay.length > 0">
					<!--  Title  -->
					<q-item-label header>
						{{ $t('components.account-selector.title') }}
					</q-item-label>

					<!--  Account Row  -->
					<q-item
						v-for="(account, index) in accountsDisplay"
						:key="index"
						v-close-popup
						clickable
						tabindex="0"
						@click="updateActiveAccountId(account.id)">
						<q-item-section class="account-selector-details">
							<q-item-label>{{ account.displayName }}</q-item-label>
							<q-item-label
								v-if="account.username"
								caption>
								{{ account.username }}
							</q-item-label>
						</q-item-section>
						<q-item-section side>
							<q-btn
								flat
								icon="mdi-refresh"
								class="account-selector-refresh"
								:loading="account.loading"
								:disabled="accountStore.accessSyncLoading"
								:data-cy="`refresh-account-${account.id}-btn`"
								:aria-label="`${t('components.account-selector.title')}: ${account.displayName}`"
								@click.stop="runReSyncAccount(account.id)" />
						</q-item-section>
					</q-item>
				</template>
				<!--	No account found -->
				<q-item-label v-else>
					{{ t('components.app-bar.no-accounts') }}
				</q-item-label>
				<q-separator />
				<!-- Log out Button -->
				<q-item
					clickable
					@click="onLogOut">
					<q-item-section>
						<q-item-label>{{ $t('components.account-selector.log-out-button') }}</q-item-label>
					</q-item-section>
					<q-item-section side>
						<q-icon name="mdi-logout" />
					</q-item-section>
				</q-item>
			</q-list>
		</q-menu>
	</q-btn>
</template>

<script setup lang="ts">
import Log from 'consola';
import { useSettingsStore, useAccountStore, useAuthenticationStore } from '@store';
import { useI18n } from 'vue-i18n';
import { tap } from 'rxjs/operators';
import { useSubscription } from '@vueuse/rxjs';

const { t } = useI18n();
const settingsStore = useSettingsStore();
const accountStore = useAccountStore();
const authStore = useAuthenticationStore();
const dialogStore = useDialogStore();

const accountsDisplay = computed(() => {
	return [
		{
			id: 0,
			displayName: t('components.account-selector.all-accounts'),
			loading: accountStore.accessSyncLoading && accountStore.accessSyncLoadingAccountId === 0,
			username: '',
		},
		...accountStore.accounts
			.filter((x) => x.isEnabled)
			.map((x) => {
				return {
					id: x.id,
					displayName: accountStore.getAccountDisplayName(x.id),
					loading: accountStore.accessSyncLoading && accountStore.accessSyncLoadingAccountId === x.id,
					username: accountStore.getAccountUserName(x.id),
				};
			}),
	];
});

function updateActiveAccountId(accountId: number): void {
	settingsStore.generalSettings.activeAccountId = accountId;
}

function runReSyncAccount(accountId = 0): void {
	useSubscription(
		accountStore
			.reSyncAccount(accountId)
			.pipe(tap((data) => {
				if (data.isSuccess) {
					dialogStore.openRefreshPlexAccountAccessDialog(data.value ?? []);
				} else {
					Log.error(`Failed to re-sync account with id ${accountId}: ${data.errors}`);
				}
			}))
			.subscribe(),
	);
}

function onLogOut(): void {
	useSubscription(authStore.logout().subscribe());
}
</script>

<style lang="scss">
.account-selector-list {
  max-width: min(100vw, 28rem);

  .account-selector-details {
    min-width: 0;

    .q-item__label {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }

  .account-selector-refresh {
    min-width: 44px;
    min-height: 44px;
  }
}
</style>
