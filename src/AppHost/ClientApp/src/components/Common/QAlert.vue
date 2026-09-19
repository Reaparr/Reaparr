<template>
	<q-item
		v-ripple
		:clickable="dismissible || to !== ''"
		:class="classConfig"
		:data-cy="cy"
		:to="to"
		class="q-ma-md">
		<div class="q-alert-wrapper">
			<q-icon
				:name="alertIcon"
				size="24px" />
			<div class="q-alert-content">
				<slot />
			</div>
			<div
				v-if="dismissible"
				class="q-alert-dismissible">
				<q-icon
					name="mdi-close-circle"
					size="24px" />
			</div>
		</div>
	</q-item>
</template>

<script setup lang="ts">
import { NotificationLevel } from '@dto';

const props = withDefaults(defineProps<{
	type?: 'error' | 'warning' | 'info' | 'success' | NotificationLevel;
	dismissible?: boolean;
	cy?: string;
	to?: string;
}>(), {
	type: 'info',
	dismissible: false,
	cy: '',
	to: '',
});

const classConfig = computed(() => {
	let normalizedType: string;

	switch (props.type) {
		case 'success':
		case NotificationLevel.Success:
			normalizedType = 'success';
			break;
		case 'info':
		case NotificationLevel.Information:
		case NotificationLevel.Debug:
			normalizedType = 'info';
			break;
		case 'warning':
		case NotificationLevel.Warning:
			normalizedType = 'warning';
			break;
		case 'error':
		case NotificationLevel.Error:
		case NotificationLevel.Fatal:
			normalizedType = 'error';
			break;
		default:
			normalizedType = 'info';
			break;
	}

	return {
		'q-alert': true,
		[`q-alert--${normalizedType}`]: true,
	};
});

const alertIcon = computed((): string => {
	switch (props.type) {
		case 'success':
		case NotificationLevel.Success:
			return 'mdi-check-circle-outline';
		case 'info':
		case NotificationLevel.Information:
		case NotificationLevel.Debug:
			return 'mdi-information-outline';
		case 'warning':
		case NotificationLevel.Warning:
			return 'mdi-alert-outline';
		case 'error':
		case NotificationLevel.Fatal:
		case NotificationLevel.Error:
			return 'mdi-alert-circle-outline';
		default:
			return 'mdi-close';
	}
});
</script>

<style lang="scss">
@use '@/assets/scss/variables.scss' as *;

.q-alert {
  display: block;
  font-size: 16px;
  margin: 1rem;
  padding: 16px;
  position: relative;
  transition: 0.3s cubic-bezier(0.25, 0.8, 0.5, 1);
  border: 1px solid;
  border-radius: 4px;

  &--success {
    border-color: $success-color;
    color: $success-color;
  }

  &--error {
    border-color: $error-color;
    color: $error-color;
  }

  &--warning {
    border-color: #ff6f00;
    color: #ff6f00;
  }

  &--info {
    border-color: #e8f5e9;
    color: #e8f5e9;
  }

  .q-alert-wrapper {
    align-items: center;
    display: flex;

    .q-alert-content {
      flex: 1 1 auto;
      padding: 0 1em;
    }

    .q-alert-dismissible {
      margin: -16px -8px -16px 8px;
    }
  }

  &:hover {
    cursor: pointer;
  }
}

@media (max-width: 1023px) {
  .q-alert {
    font-size: clamp(0.875rem, 0.82rem + 0.2vw, 1rem);
  }
}
</style>
