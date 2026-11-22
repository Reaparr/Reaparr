<template>
	<QSection header="Notification Debug">
		<QRow class="q-gutter-sm">
			<QCol cols="auto">
				<q-btn
					color="negative"
					label="Error"
					@click="triggerError" />
			</QCol>
			<QCol cols="auto">
				<q-btn
					color="negative"
					outline
					label="Error (5s)"
					@click="triggerErrorWithTimeout" />
			</QCol>
			<QCol cols="auto">
				<q-btn
					color="positive"
					label="Success"
					@click="triggerSuccess" />
			</QCol>
			<QCol cols="auto">
				<q-btn
					color="positive"
					outline
					label="Success (5s)"
					@click="triggerSuccessWithTimeout" />
			</QCol>
		</QRow>
	</QSection>
</template>

<script setup lang="ts">
import { showErrorNotification, showSuccessNotification } from '#imports';

function triggerError() {
	showErrorNotification('Debug error notification');
}
function triggerErrorWithTimeout() {
	showErrorNotification('Debug error notification (5 seconds)', 5000);
}
function triggerSuccess() {
	showSuccessNotification('Debug success notification');
}
function triggerSuccessWithTimeout() {
	showSuccessNotification('Debug success notification (5 seconds)', 5000);
}
</script>
