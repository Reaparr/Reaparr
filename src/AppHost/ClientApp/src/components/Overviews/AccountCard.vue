<template>
	<!-- Edit Account -->
	<OverviewCard
		v-if="account"
		mode="edit"
		class="account-card"
		:cy="`account-card-id-${account.id}`"
		icon="plex"
		:title="accountStore.getAccountDisplayName(account.id)"
		:chips="accountChips"
		@click="$emit('open-dialog', account)" />
	<!-- Add new account -->
	<OverviewCard
		v-else-if="isNew"
		mode="add"
		class="account-card"
		cy="account-overview-add-account"
		@click="$emit('open-dialog', null)" />
	<!-- Account was invalid -->
	<q-card v-else>
		<q-card-section>
			<span>{{ t('components.account-card.invalid-account') }}</span>
		</q-card-section>
	</q-card>
</template>

<script setup lang="ts">
import type { PlexAccountDTO } from '@dto';
import { useAccountStore } from '@store';

const props = defineProps<{
	account?: PlexAccountDTO;
}>();

const { t } = useI18n();
const accountStore = useAccountStore();

const isNew = computed(() => !props.account);
const accountChips = computed<Array<{ value: string; color: 'positive' | 'negative' }>>(() => {
	if (!props.account) return [];

	return [
		{
			value: props.account.isValidated ? t('general.commands.validated') : t('general.commands.not-validated'),
			color: props.account.isValidated ? 'positive' : 'negative',
		},
		{
			value: props.account.isEnabled ? t('general.commands.enabled') : t('general.commands.disabled'),
			color: props.account.isEnabled ? 'positive' : 'negative',
		},
	];
});

defineEmits<{ (e: 'open-dialog', account: PlexAccountDTO | null): void }>();
</script>
