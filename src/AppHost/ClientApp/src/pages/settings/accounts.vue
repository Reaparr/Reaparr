<template>
	<QPage class="settings-page">
		<QSection :header="$t('pages.settings.accounts.plex-accounts-header')">
			<AccountOverview />
		</QSection>
		<QSection :header="$t('pages.settings.accounts.app-accounts-header')">
			<AuthenticationOverview />
		</QSection>
		<QSection :header="$t('pages.settings.accounts.library-access-history')">
			<!-- Library Access History -->
			<QAlert to="/library/access-history">
				{{ $t('pages.settings.accounts.library-access-history-link-text') }}
			</QAlert>
		</QSection>
	</QPage>
</template>
