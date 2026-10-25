import { IntegrationPaths } from '@api/generated/Integration';
import { route } from '@fixtures';
import {
	IntegrationProvisioningState,
	IntegrationSetupProgressStage,
	IntegrationType,
	MessageTypes,
	TestConnectionStatus,
	type IntegrationSetupProgressDTO,
	type IntegrationSummary,
	type SonarrIntegrationDTO,
	type UpdateSonarrIntegrationRequest,
} from '@dto';
import { generateResultDTO } from '@mock';

const validationError = 'Failed to validate download client; [ { "errorMessage": "Unable to connect to qBittorrent" } ]; Failed to validate indexer; [ { "errorMessage": "Unable to connect to indexer" } ]';

const integrationId = '00000000-0000-0000-0000-000000000001';
const createdIntegrationId = '00000000-0000-0000-0000-000000000002';
const integrationName = 'Sonarr';
const integrationUrl = 'http://localhost:8989';
const integrationApiKey = '0123456789abcdef0123456789abcdef';
const integrationCategory = 'sonarr';
const updatedName = 'Sonarr Updated';
const updatedUrl = 'http://localhost:9899';
const testedAt = '2026-01-01T00:00:00.000Z';
const createRequest = {
	name: 'Sonarr Created',
	url: 'http://localhost:9898',
	apiKey: integrationApiKey,
	category: 'sonarr-created',
	downloadFolderId: 1,
};
const unconfiguredSummary: IntegrationSummary = {
	id: integrationId,
	type: IntegrationType.Sonarr,
	name: integrationName,
	baseUrl: integrationUrl,
	category: integrationCategory,
	provisioningState: IntegrationProvisioningState.Unconfigured,
	lastConnectionTestStatus: TestConnectionStatus.Unknown,
	downloadFolderId: 0,
	externalDownloadClientId: null,
	externalIndexerId: null,
};
const unconfiguredDetail: SonarrIntegrationDTO = {
	id: integrationId,
	name: integrationName,
	url: integrationUrl,
	apiKey: integrationApiKey,
	category: integrationCategory,
	downloadFolderId: 0,
	provisioningState: IntegrationProvisioningState.Unconfigured,
	lastConnectionTestStatus: TestConnectionStatus.Unknown,
};
const setupStages = [
	{
		stage: IntegrationSetupProgressStage.Connecting,
		selector: 'connect',
		error: 'Sonarr connection failed',
		successText: 'Connected.',
	},
	{
		stage: IntegrationSetupProgressStage.DownloadClient,
		selector: 'download-client',
		error: 'Sonarr download client setup failed',
		successText: 'Download client in Sonarr ready.',
	},
	{
		stage: IntegrationSetupProgressStage.Indexer,
		selector: 'indexer',
		error: 'Sonarr indexer setup failed',
		successText: 'Indexer client in Sonarr ready.',
	},
	{
		stage: IntegrationSetupProgressStage.Validation,
		selector: 'validation',
		error: validationError,
		successText: 'Sonarr can communicate back to Reaparr.',
	},
	{
		stage: IntegrationSetupProgressStage.Done,
		selector: 'done',
		error: '',
	},
] as const;

type SonarrUrlScenario = {
	invalidUrls: readonly string[];
	invalidUrlMessage: string;
	enteredValidUrl: string;
	persistedUrl: string;
	currentDetail: SonarrIntegrationDTO;
	currentSummary: IntegrationSummary;
	updateCallCount: number;
	testConnectionCallCount: number;
};

function createSonarrUrlScenario(): SonarrUrlScenario {
	return {
		invalidUrls: [
			'ftp://sonarr.example.test:9899/sonarr',
			'not-a-valid-url',
		],
		invalidUrlMessage: 'URL must be an absolute http/https URL.',
		enteredValidUrl: '  https://sonarr.example.test:9899/sonarr/  ',
		persistedUrl: 'https://sonarr.example.test:9899/sonarr',
		currentDetail: {
			...unconfiguredDetail,
			downloadFolderId: 1,
			provisioningState: IntegrationProvisioningState.Configured,
			lastConnectionTestStatus: TestConnectionStatus.Success,
			lastConnectionTestHttpStatusCode: 200,
			lastConnectionTestErrorMessage: null,
			lastConnectionTestedAt: testedAt,
		},
		currentSummary: {
			...unconfiguredSummary,
			downloadFolderId: 1,
			provisioningState: IntegrationProvisioningState.Configured,
			lastConnectionTestStatus: TestConnectionStatus.Success,
			lastConnectionTestHttpStatusCode: 200,
			lastConnectionTestErrorMessage: null,
			lastConnectionTestedAt: testedAt,
		},
		updateCallCount: 0,
		testConnectionCallCount: 0,
	};
}

function installSonarrUrlReadApis(scenario: SonarrUrlScenario): void {
	cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), (request) => {
		request.reply({ statusCode: 200, body: generateResultDTO([scenario.currentSummary]) });
	}).as('getIntegrations');
	cy.intercept('GET', IntegrationPaths.getSonarrIntegrationEndpoint(integrationId), (request) => {
		request.reply({ statusCode: 200, body: generateResultDTO(scenario.currentDetail) });
	}).as('getSonarr');
}

function installSonarrUrlTestApi(scenario: SonarrUrlScenario): void {
	cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToSonarrEndpoint() }, (request) => {
		scenario.testConnectionCallCount++;
		request.reply({
			statusCode: 200,
			body: generateResultDTO({
				result: TestConnectionStatus.Success,
				errorMessage: null,
				httpStatusCode: 200,
				testedAt,
			}),
		});
	}).as('testSonarrConnection');
}

function installSonarrUrlMutationApi(scenario: SonarrUrlScenario): void {
	cy.intercept('PUT', IntegrationPaths.updateSonarrIntegrationEndpoint(integrationId), (request) => {
		const body = request.body as UpdateSonarrIntegrationRequest;
		const normalizedUrl = body.url.trim().replace(/\/+$/, '');
		scenario.updateCallCount++;
		scenario.currentDetail = {
			...scenario.currentDetail,
			...body,
			url: normalizedUrl,
			provisioningState: IntegrationProvisioningState.ChangesPending,
			lastConnectionTestStatus: TestConnectionStatus.Unknown,
			lastConnectionTestHttpStatusCode: null,
			lastConnectionTestErrorMessage: null,
			lastConnectionTestedAt: null,
		};
		scenario.currentSummary = {
			...scenario.currentSummary,
			name: body.name,
			baseUrl: normalizedUrl,
			category: body.category,
			downloadFolderId: body.downloadFolderId,
			provisioningState: scenario.currentDetail.provisioningState,
			lastConnectionTestStatus: scenario.currentDetail.lastConnectionTestStatus,
			lastConnectionTestHttpStatusCode: null,
			lastConnectionTestErrorMessage: null,
			lastConnectionTestedAt: null,
		};
		request.reply({ statusCode: 200, body: generateResultDTO(scenario.currentDetail) });
	}).as('updateSonarr');
}

function installSonarrUrlApis(scenario: SonarrUrlScenario): void {
	cy.basePageSetup({ plexAccountCount: 0, plexServerCount: 0 });
	installSonarrUrlReadApis(scenario);
	installSonarrUrlTestApi(scenario);
	installSonarrUrlMutationApi(scenario);
}

function openSonarrUrlEditDialog(): void {
	cy.visit(route('/settings/integrations'));
	cy.getPageData();
	cy.wait('@getIntegrations');
	cy.getCy('integration-card').should('contain.text', 'Connected').click();
	cy.wait('@getSonarr');
}

function assertInvalidSonarrUrls(scenario: SonarrUrlScenario): void {
	for (const invalidUrl of scenario.invalidUrls) {
		cy.getCy('integration-base-url').clear().type(invalidUrl).blur();
		cy.getCy('integration-base-url')
			.closest('.q-field')
			.should('contain.text', scenario.invalidUrlMessage);
		cy.getCy('integration-save').should('be.disabled');
		cy.getCy('integration-test').should('be.disabled');
		cy.getCy('integration-setup').should('be.disabled');
		cy.then(() => {
			expect(scenario.updateCallCount).to.equal(0);
			expect(scenario.testConnectionCallCount).to.equal(0);
		});
	}
}

function enterValidSonarrUrl(scenario: SonarrUrlScenario): void {
	cy.getCy('integration-base-url').clear().type(scenario.enteredValidUrl).blur();
	cy.getCy('integration-base-url').should('have.value', scenario.enteredValidUrl);
	cy.getCy('integration-base-url').closest('.q-field').should('not.contain.text', scenario.invalidUrlMessage);
	cy.getCy('integration-save').should('not.be.disabled');
	cy.getCy('integration-test').should('not.be.disabled');
	cy.getCy('integration-setup').should('be.disabled');
}

function saveSonarrUrl(scenario: SonarrUrlScenario): void {
	cy.getCy('integration-save').click();
	cy.wait('@updateSonarr').then(({ request, response }) => {
		expect(request.method).to.equal('PUT');
		expect(request.body).to.deep.equal({
			name: integrationName,
			url: scenario.enteredValidUrl,
			apiKey: integrationApiKey,
			category: integrationCategory,
			downloadFolderId: 1,
		});
		expect(response?.statusCode).to.equal(200);
		expect(response?.body.value).to.deep.include({
			url: scenario.persistedUrl,
		});
	});
	cy.wait('@getIntegrations');
	cy.then(() => {
		expect(scenario.updateCallCount).to.equal(1);
		expect(scenario.testConnectionCallCount).to.equal(0);
	});
}

function assertSavedSonarrUrl(scenario: SonarrUrlScenario): void {
	cy.getCy('integration-dialog').should('not.exist');
	cy.getCy('integration-card')
		.should('be.visible')
		.and('contain.text', scenario.persistedUrl)
		.and('contain.text', 'Unconnected')
		.and('contain.text', IntegrationProvisioningState.ChangesPending);
	cy.getCy('integration-card').click();
	cy.wait('@getSonarr');
	cy.getCy('integration-base-url').should('have.value', scenario.persistedUrl);
	cy.getCy('integration-dialog').find('[data-cy="dialog-close-button"]').click();
}

describe('Sonarr integrations', () => {
	it('rejects non-http and malformed Sonarr URLs before saving, then saves a valid offline URL', () => {
		const scenario = createSonarrUrlScenario();
		installSonarrUrlApis(scenario);
		openSonarrUrlEditDialog();
		assertInvalidSonarrUrls(scenario);
		enterValidSonarrUrl(scenario);
		saveSonarrUrl(scenario);
		assertSavedSonarrUrl(scenario);
	});

	it('clears a successful connection result when the URL changes', () => {
		const changedUrl = 'https://sonarr.example.test:9899/sonarr';
		const currentDetail: SonarrIntegrationDTO = {
			...unconfiguredDetail,
			downloadFolderId: 1,
			provisioningState: IntegrationProvisioningState.Configured,
			lastConnectionTestStatus: TestConnectionStatus.Unknown,
		};
		const currentSummary: IntegrationSummary = {
			...unconfiguredSummary,
			downloadFolderId: 1,
			provisioningState: IntegrationProvisioningState.Configured,
			lastConnectionTestStatus: TestConnectionStatus.Unknown,
		};

		cy.basePageSetup({ plexAccountCount: 0, plexServerCount: 0 });
		cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), (request) => {
			request.reply({ statusCode: 200, body: generateResultDTO([currentSummary]) });
		}).as('getIntegrations');
		cy.intercept('GET', IntegrationPaths.getSonarrIntegrationEndpoint(integrationId), (request) => {
			request.reply({ statusCode: 200, body: generateResultDTO(currentDetail) });
		}).as('getSonarr');
		cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToSonarrEndpoint() }, {
			statusCode: 200,
			body: generateResultDTO({
				result: TestConnectionStatus.Success,
				errorMessage: null,
				httpStatusCode: 200,
				testedAt,
			}),
		}).as('testSonarrConnection');

		cy.visit(route('/settings/integrations'));
		cy.getPageData();
		cy.wait('@getIntegrations');
		cy.getCy('integration-card').click();
		cy.wait('@getSonarr');
		cy.getCy('integration-test').click();
		cy.wait('@testSonarrConnection').then(({ request }) => {
			expect(request.query).to.deep.include({
				url: integrationUrl,
				apiKey: integrationApiKey,
				integrationId,
			});
		});
		cy.getCy('integration-dialog').should('contain.text', 'Connection successful');
		cy.getCy('integration-base-url').clear().type(changedUrl).blur();
		cy.getCy('integration-dialog').should('not.contain.text', 'Connection successful');
		cy.getCy('integration-save').should('not.be.disabled');
		cy.getCy('integration-setup').should('be.disabled');
		cy.getCy('integration-dialog').find('[data-cy="dialog-close-button"]').click();
		cy.getCy('integration-dialog').should('not.exist');
	});

	it('checks and saves a new Sonarr integration as explicit separate actions', () => {
		let integrations: IntegrationSummary[] = [];
		let saveCallCount = 0;
		const createdDetail: SonarrIntegrationDTO = {
			id: createdIntegrationId,
			name: createRequest.name,
			url: createRequest.url,
			apiKey: createRequest.apiKey,
			category: createRequest.category,
			downloadFolderId: createRequest.downloadFolderId,
			provisioningState: IntegrationProvisioningState.Unconfigured,
			lastConnectionTestStatus: TestConnectionStatus.Success,
		};
		const createdSummary: IntegrationSummary = {
			id: createdIntegrationId,
			type: IntegrationType.Sonarr,
			name: createRequest.name,
			baseUrl: createRequest.url,
			category: createRequest.category,
			provisioningState: IntegrationProvisioningState.Unconfigured,
			lastConnectionTestStatus: TestConnectionStatus.Success,
			downloadFolderId: createRequest.downloadFolderId,
			externalDownloadClientId: null,
			externalIndexerId: null,
		};

		cy.basePageSetup({ plexAccountCount: 0, plexServerCount: 0 });
		cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), (request) => {
			request.reply({ statusCode: 200, body: generateResultDTO(integrations) });
		}).as('getIntegrations');
		cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToSonarrEndpoint() }, {
			delay: 1000,
			statusCode: 200,
			body: generateResultDTO({
				result: TestConnectionStatus.Success,
				errorMessage: null,
				httpStatusCode: 200,
				testedAt,
			}),
		}).as('testSonarrConnection');
		cy.intercept('POST', IntegrationPaths.createSonarrIntegrationEndpoint(), (request) => {
			saveCallCount++;
			integrations = [createdSummary];
			request.reply({ delay: 1000, statusCode: 200, body: generateResultDTO(createdDetail) });
		}).as('createSonarr');
		cy.visit(route('/settings/integrations'));
		cy.wait('@getIntegrations');

		cy.getCy('add-integration').click();
		cy.getCy('integration-dialog').should('be.visible');
		cy.getCy('integration-type-sonarr').click();

		cy.getCy('integration-name').focus().blur();
		cy.getCy('integration-name').closest('.q-field').should('contain.text', 'Required');
		cy.getCy('integration-base-url').focus().blur();
		cy.getCy('integration-base-url').closest('.q-field').should('contain.text', 'Required');
		cy.getCy('integration-arr-key').focus().blur();
		cy.getCy('integration-arr-key').closest('.q-field').should('contain.text', 'Required');
		cy.getCy('integration-category').clear().blur();
		cy.getCy('integration-category').closest('.q-field').should('contain.text', 'Required');
		cy.getCy('integration-save').should('be.disabled');
		cy.getCy('integration-test').should('be.disabled');
		cy.then(() => expect(saveCallCount).to.equal(0));

		cy.getCy('integration-name').type(createRequest.name);
		cy.getCy('integration-base-url').type(createRequest.url);
		cy.getCy('integration-arr-key').type(createRequest.apiKey);
		cy.getCy('integration-category').type(createRequest.category);
		cy.getCy('integration-download-folder').click();
		cy.get('.q-menu').contains('.q-item', '/Downloads').click();
		cy.getCy('integration-save').should('not.be.disabled');
		cy.getCy('integration-test').should('not.be.disabled');

		cy.getCy('integration-test').click();
		cy.getCy('integration-test').find('.q-spinner').should('exist');
		cy.getCy('integration-save').find('.q-spinner').should('not.exist');
		cy.then(() => expect(saveCallCount).to.equal(0));
		cy.wait('@testSonarrConnection').then(({ request, response }) => {
			expect(request.method).to.equal('GET');
			expect(request.query).to.deep.include({ apiKey: createRequest.apiKey, url: createRequest.url });
			expect(response?.statusCode).to.equal(200);
		});
		cy.then(() => expect(saveCallCount).to.equal(0));
		cy.getCy('integration-dialog')
			.should('contain.text', 'Connection successful')
			.and('contain.text', 'HTTP 200');

		cy.getCy('integration-dialog')
			.should('be.visible')
			.and('contain.text', 'Add Sonarr integration');
		cy.getCy('integration-delete').should('not.exist');
		cy.getCy('integration-setup')
			.should('not.be.disabled')
			.and('contain.text', 'Save & Setup');
		cy.getCy('integration-save').click();
		cy.getCy('integration-save').find('.q-spinner').should('exist');
		cy.getCy('integration-test').find('.q-spinner').should('not.exist');
		cy.wait('@createSonarr').then(({ request, response }) => {
			expect(request.method).to.equal('POST');
			expect(request.body).to.deep.equal(createRequest);
			expect(response?.statusCode).to.equal(200);
			expect(response?.body.value).to.deep.include(createdDetail);
		});
		cy.wait('@getIntegrations');
		cy.then(() => expect(saveCallCount).to.equal(1));
		cy.getCy('integration-dialog').should('not.exist');
		cy.getCy('integration-card')
			.should('have.length', 1)
			.and('be.visible')
			.and('have.attr', 'aria-label', createRequest.name)
			.and('contain.text', createRequest.name)
			.and('contain.text', createRequest.url)
			.and('contain.text', IntegrationProvisioningState.Unconfigured);
		cy.getCy('integration-card').find('[alt="sonarr"]').should('exist');
	});

	it('saves and sets up a new Sonarr integration before closing both dialogs', () => {
		let integrations: IntegrationSummary[] = [];
		let createCallCount = 0;
		let setupCallCount = 0;
		const createdDetail: SonarrIntegrationDTO = {
			id: createdIntegrationId,
			name: createRequest.name,
			url: createRequest.url,
			apiKey: createRequest.apiKey,
			category: createRequest.category,
			downloadFolderId: createRequest.downloadFolderId,
			provisioningState: IntegrationProvisioningState.Unconfigured,
			lastConnectionTestStatus: TestConnectionStatus.Success,
		};
		const createdSummary: IntegrationSummary = {
			id: createdIntegrationId,
			type: IntegrationType.Sonarr,
			name: createRequest.name,
			baseUrl: createRequest.url,
			category: createRequest.category,
			provisioningState: IntegrationProvisioningState.Unconfigured,
			lastConnectionTestStatus: TestConnectionStatus.Success,
			downloadFolderId: createRequest.downloadFolderId,
			externalDownloadClientId: null,
			externalIndexerId: null,
		};
		const configuredDetail = { ...createdDetail, provisioningState: IntegrationProvisioningState.Configured };

		cy.basePageSetup({ plexAccountCount: 0, plexServerCount: 0 });
		cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), (request) => {
			request.reply({ statusCode: 200, body: generateResultDTO(integrations) });
		}).as('getIntegrations');
		cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToSonarrEndpoint() }, {
			statusCode: 200,
			body: generateResultDTO({
				result: TestConnectionStatus.Success,
				errorMessage: null,
				httpStatusCode: 200,
				testedAt,
			}),
		}).as('testSonarrConnection');
		cy.intercept('POST', IntegrationPaths.createSonarrIntegrationEndpoint(), (request) => {
			createCallCount++;
			integrations = [createdSummary];
			request.reply({ delay: 1000, statusCode: 200, body: generateResultDTO(createdDetail) });
		}).as('createSonarr');
		cy.intercept('POST', IntegrationPaths.setupSonarrIntegrationEndpoint(createdIntegrationId), (request) => {
			setupCallCount++;
			request.reply({ statusCode: 200, body: generateResultDTO(configuredDetail) });
		}).as('setupSonarr');
		cy.visit(route('/settings/integrations'));
		cy.wait('@getIntegrations');

		cy.getCy('add-integration').click();
		cy.getCy('integration-type-sonarr').click();
		cy.getCy('integration-name').type(createRequest.name);
		cy.getCy('integration-base-url').type(createRequest.url);
		cy.getCy('integration-arr-key').type(createRequest.apiKey);
		cy.getCy('integration-category').clear().type(createRequest.category);
		cy.getCy('integration-download-folder').click();
		cy.get('.q-menu').contains('.q-item', '/Downloads').click();
		cy.getCy('integration-test').click();
		cy.wait('@testSonarrConnection');
		cy.then(() => {
			expect(createCallCount).to.equal(0);
			expect(setupCallCount).to.equal(0);
		});

		cy.getCy('integration-setup')
			.should('contain.text', 'Save & Setup')
			.and('not.be.disabled')
			.click();
		cy.getCy('integration-setup').find('.q-spinner').should('exist');
		cy.getCy('integration-save').find('.q-spinner').should('not.exist');
		cy.wait('@createSonarr').then(({ request }) => {
			expect(request.body).to.deep.equal(createRequest);
		});
		cy.wait('@getIntegrations');
		cy.wait('@setupSonarr').then(({ request }) => {
			expect(request.url).to.contain(createdIntegrationId);
		});
		cy.then(() => {
			expect(createCallCount).to.equal(1);
			expect(setupCallCount).to.equal(1);
		});
		cy.getCy('integration-setup-dialog').should('be.visible');
		cy.getCy('integration-dialog')
			.should('exist')
			.and('contain.text', 'Add Sonarr integration');
		cy.getCy('integration-setup-dialog').find('[data-cy="dialog-close-button"]').click();
		cy.getCy('integration-setup-dialog').should('not.exist');
		cy.getCy('integration-dialog').should('not.exist');
	});

	it('edits and sets up an existing unconfigured Sonarr integration', () => {
		let currentSummary = unconfiguredSummary;
		let currentDetail = unconfiguredDetail;
		let updateCallCount = 0;
		let setupCallCount = 0;
		let testConnectionCallCount = 0;
		let isDeleted = false;
		const updateRequest = {
			name: updatedName,
			url: updatedUrl,
			apiKey: integrationApiKey,
			category: integrationCategory,
			downloadFolderId: 1,
		};
		const configuredSummary: IntegrationSummary = {
			...unconfiguredSummary,
			name: updatedName,
			downloadFolderId: 1,
			provisioningState: IntegrationProvisioningState.Configured,
			externalDownloadClientId: 1,
			externalIndexerId: 2,
			lastConnectionTestStatus: TestConnectionStatus.Success,
			lastConnectionTestHttpStatusCode: 200,
			lastConnectionTestErrorMessage: null,
			lastConnectionTestedAt: testedAt,
		};
		const configuredDetail: SonarrIntegrationDTO = {
			...unconfiguredDetail,
			name: updatedName,
			downloadFolderId: 1,
			provisioningState: IntegrationProvisioningState.Configured,
			lastConnectionTestStatus: TestConnectionStatus.Success,
			lastConnectionTestHttpStatusCode: 200,
			lastConnectionTestErrorMessage: null,
			lastConnectionTestedAt: testedAt,
		};

		cy.basePageSetup({ plexAccountCount: 0, plexServerCount: 0 });
		cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), (request) => {
			request.reply({ statusCode: 200, body: generateResultDTO(isDeleted ? [] : [currentSummary]) });
		}).as('getIntegrations');
		cy.intercept('GET', IntegrationPaths.getSonarrIntegrationEndpoint(integrationId), {
			statusCode: 200,
			body: generateResultDTO(currentDetail),
		}).as('getSonarr');
		cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToSonarrEndpoint() }, (request) => {
			testConnectionCallCount++;
			const result = testConnectionCallCount === 1
				? {
						result: TestConnectionStatus.ConnectionFailed,
						errorMessage: 'Connection refused',
						httpStatusCode: null,
						testedAt,
					}
				: {
						result: TestConnectionStatus.Success,
						errorMessage: null,
						httpStatusCode: 200,
						testedAt,
					};
			request.reply({ statusCode: 200, body: generateResultDTO(result) });
		}).as('testSonarrConnection');
		cy.intercept('PUT', IntegrationPaths.updateSonarrIntegrationEndpoint(integrationId), (request) => {
			updateCallCount++;
			currentSummary = { ...currentSummary, name: request.body.name, baseUrl: request.body.url, category: request.body.category, downloadFolderId: request.body.downloadFolderId };
			currentDetail = { ...currentDetail, name: request.body.name, url: request.body.url, apiKey: request.body.apiKey, category: request.body.category, downloadFolderId: request.body.downloadFolderId };
			request.reply({ statusCode: 200, body: generateResultDTO(currentDetail) });
		}).as('updateSonarr');
		cy.intercept('POST', IntegrationPaths.setupSonarrIntegrationEndpoint(integrationId), (request) => {
			setupCallCount++;
			currentSummary = configuredSummary;
			currentDetail = configuredDetail;
			request.reply({ statusCode: 200, body: generateResultDTO(configuredDetail) });
		}).as('setupSonarr');
		cy.intercept('DELETE', IntegrationPaths.deleteSonarrIntegrationEndpoint(integrationId, { force: true }), (request) => {
			isDeleted = true;
			request.reply({ delay: 1000, statusCode: 204 });
		}).as('deleteSonarr');
		cy.visit(route('/settings/integrations'));
		cy.wait('@getIntegrations');

		cy.getCy('integration-card').click();
		cy.wait('@getSonarr');
		cy.getCy('integration-name').should('have.value', integrationName);
		cy.getCy('integration-download-folder').click();
		cy.getCy('integration-name').click();
		cy.getCy('integration-download-folder').closest('.q-field').should('contain.text', 'Required');
		cy.getCy('integration-save').should('be.disabled');
		cy.getCy('integration-download-folder').click();
		cy.get('.q-menu').contains('.q-item', '/Downloads').click();
		cy.getCy('integration-base-url').invoke('val', updatedUrl).trigger('input').trigger('change');
		cy.getCy('integration-base-url').should('have.value', updatedUrl);
		cy.getCy('integration-test').click();
		cy.wait('@testSonarrConnection').then(({ request, response }) => {
			expect(request.query).to.deep.include({ apiKey: integrationApiKey, url: updatedUrl, integrationId });
			expect(response?.statusCode).to.equal(200);
		});
		cy.getCy('integration-dialog').should('contain.text', 'Connection failed').and('contain.text', `URL attempted: ${updatedUrl}`);
		cy.get('[data-cy="q-card-dialog-cy"]').should('not.exist');
		cy.getCy('integration-test').click();
		cy.wait('@testSonarrConnection').then(({ request, response }) => {
			expect(request.query).to.deep.include({ apiKey: integrationApiKey, url: updatedUrl, integrationId });
			expect(response?.statusCode).to.equal(200);
		});
		cy.getCy('integration-dialog').should('contain.text', 'Connection successful');
		cy.getCy('integration-name').invoke('val', updatedName).trigger('input').trigger('change');
		cy.getCy('integration-name').should('have.value', updatedName);
		cy.getCy('integration-setup').should('contain.text', 'Save & Setup');
		cy.getCy('integration-save').click();
		cy.wait('@updateSonarr').then(({ request, response }) => {
			expect(request.method).to.equal('PUT');
			expect(request.body).to.deep.equal(updateRequest);
			expect(response?.statusCode).to.equal(200);
		});
		cy.wait('@getIntegrations');
		cy.then(() => expect(updateCallCount).to.equal(1));
		cy.getCy('integration-dialog').should('not.exist');
		cy.getCy('integration-card')
			.should('contain.text', updatedName)
			.and('contain.text', IntegrationProvisioningState.Unconfigured)
			.and('contain.text', 'Connected');

		cy.getCy('integration-card').click();
		cy.wait('@getSonarr');
		cy.getCy('integration-setup').should('contain.text', 'Setup').and('not.contain.text', 'Save & Setup').click();
		for (const { selector } of setupStages) {
			cy.getCy(`integration-setup-step-${selector}`).should('have.attr', 'data-status', 'pending');
		}

		for (const [index, stageDefinition] of setupStages.entries()) {
			const { stage, selector, error } = stageDefinition;
			if (stage === IntegrationSetupProgressStage.Done) {
				cy.hubPublish('progress', MessageTypes.IntegrationSetupProgress, {
					integrationId,
					stage,
					isRunning: false,
					isSuccess: true,
				} satisfies IntegrationSetupProgressDTO);
				cy.getCy(`integration-setup-step-${selector}`)
					.should('have.attr', 'data-status', 'success')
					.and('not.contain.text', 'Integration setup complete.')
					.find('.q-stepper__tab')
					.should('have.class', 'text-positive')
					.and('not.have.class', 'text-negative');
				continue;
			}

			cy.hubPublish('progress', MessageTypes.IntegrationSetupProgress, {
				integrationId,
				stage,
				isRunning: true,
				isSuccess: false,
			} satisfies IntegrationSetupProgressDTO);
			cy.getCy(`integration-setup-step-${selector}`)
				.should('have.attr', 'data-status', 'running')
				.find('.q-stepper__tab')
				.should('have.class', 'text-info')
				.and('not.have.class', 'text-positive')
				.and('not.have.class', 'text-negative');

			cy.hubPublish('progress', MessageTypes.IntegrationSetupProgress, {
				integrationId,
				stage,
				isRunning: false,
				isSuccess: false,
				error,
			} satisfies IntegrationSetupProgressDTO);
			const step = cy.getCy(`integration-setup-step-${selector}`)
				.should('have.attr', 'data-status', 'error');
			if (stage === IntegrationSetupProgressStage.Validation) {
				step
					.and('contain.text', 'Failed to validate download client: Unable to connect to qBittorrent')
					.and('contain.text', 'Failed to validate indexer: Unable to connect to indexer')
					.and('not.contain.text', '"errorMessage"');
			} else {
				step.and('contain.text', error);
			}
			step.find('.q-stepper__tab')
				.should('have.class', 'text-negative')
				.and('not.have.class', 'text-positive');
			if (stage === IntegrationSetupProgressStage.Connecting) {
				cy.hubPublish('progress', MessageTypes.IntegrationSetupProgress, {
					integrationId,
					stage: IntegrationSetupProgressStage.DownloadClient,
					isRunning: false,
					isSuccess: false,
					error: 'Ignored after connection failure',
				} satisfies IntegrationSetupProgressDTO);
				cy.getCy('integration-setup-step-connect')
					.should('have.attr', 'data-status', 'error')
					.find('.q-stepper__tab')
					.should('have.class', 'text-negative');
				cy.getCy('integration-setup-step-download-client').should('have.attr', 'data-status', 'pending');
			}
			for (const { selector: previousSelector } of setupStages.slice(0, index)) {
				cy.getCy(`integration-setup-step-${previousSelector}`)
					.should('have.attr', 'data-status', 'success')
					.find('.q-stepper__tab')
					.should('have.class', 'text-positive')
					.and('not.have.class', 'text-negative');
			}
			for (const { selector: nextSelector } of setupStages.slice(index + 1)) {
				cy.getCy(`integration-setup-step-${nextSelector}`).should('have.attr', 'data-status', 'pending');
			}

			cy.hubPublish('progress', MessageTypes.IntegrationSetupProgress, {
				integrationId,
				stage,
				isRunning: false,
				isSuccess: true,
			} satisfies IntegrationSetupProgressDTO);
			cy.getCy(`integration-setup-step-${selector}`)
				.should('have.attr', 'data-status', 'success')
				.and('contain.text', stageDefinition.successText)
				.find('.q-stepper__tab')
				.should('have.class', 'text-positive')
				.and('not.have.class', 'text-negative');
		}

		cy.wait('@setupSonarr').then(({ request, response }) => {
			expect(request.method).to.equal('POST');
			expect(response?.statusCode).to.equal(200);
			expect(response?.body.value).to.deep.include(configuredDetail);
		});
		cy.wait('@getIntegrations');
		cy.then(() => expect(setupCallCount).to.equal(1));
		cy.getCy('integration-setup-dialog').find('[data-cy="dialog-close-button"]').click();
		cy.getCy('integration-setup-dialog').should('not.exist');
		cy.wait('@getIntegrations');
		cy.getCy('integration-setup').click();
		cy.wait('@setupSonarr');
		for (const { selector } of setupStages) {
			cy.getCy(`integration-setup-step-${selector}`).should('have.attr', 'data-status', 'pending');
		}
		cy.getCy('integration-setup-dialog').find('[data-cy="dialog-close-button"]').click();
		cy.getCy('integration-setup-dialog').should('not.exist');
		cy.wait('@getIntegrations');
		cy.then(() => expect(setupCallCount).to.equal(2));
		cy.getCy('integration-delete').click();
		cy.getCy('confirmation-dialog').should('be.visible');
		cy.getCy('confirmation-dialog-confirmation-button').click();
		cy.getCy('confirmation-dialog').find('.q-spinner').should('exist');
		cy.getCy('confirmation-dialog-cancel-button').should('be.disabled');
		cy.wait('@deleteSonarr').then(({ request, response }) => {
			expect(request.method).to.equal('DELETE');
			expect(request.query).to.deep.include({ force: 'true' });
			expect(response?.statusCode).to.equal(204);
		});
		cy.getCy('confirmation-dialog').should('not.exist');
		cy.getCy('integration-dialog').should('not.exist');
		cy.wait('@getIntegrations');
		cy.getCy('integration-card').should('not.exist');
	});
});
