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
	type RadarrIntegrationDTO,
	type CreateRadarrIntegrationRequest,
} from '@dto';
import { generateResultDTO } from '@mock';

const integrationId = '00000000-0000-0000-0000-000000000101';
const createdIntegrationId = '00000000-0000-0000-0000-000000000102';
const integrationName = 'Radarr';
const integrationUrl = 'http://localhost:7878';
const integrationApiKey = 'fedcba9876543210fedcba9876543210';
const integrationCategory = 'radarr';
const updatedName = 'Radarr Updated';
const updatedUrl = 'http://localhost:7978';
const testedAt = '2026-01-01T00:00:00.000Z';
const createRequest = {
	name: 'Radarr Created',
	url: 'http://localhost:7979',
	apiKey: integrationApiKey,
	category: 'radarr-created',
	downloadFolderId: 1,
};
const unconfiguredSummary: IntegrationSummary = {
	id: integrationId,
	type: IntegrationType.Radarr,
	name: integrationName,
	baseUrl: integrationUrl,
	category: integrationCategory,
	provisioningState: IntegrationProvisioningState.Unconfigured,
	lastConnectionTestStatus: TestConnectionStatus.Unknown,
	downloadFolderId: 0,
	externalDownloadClientId: null,
	externalIndexerId: null,
};
const unconfiguredDetail: RadarrIntegrationDTO = {
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
		error: 'Radarr connection failed',
		successText: 'Connected.',
	},
	{
		stage: IntegrationSetupProgressStage.DownloadClient,
		selector: 'download-client',
		error: 'Radarr download client setup failed',
		successText: 'Download client in Radarr ready.',
	},
	{
		stage: IntegrationSetupProgressStage.Indexer,
		selector: 'indexer',
		error: 'Radarr indexer setup failed',
		successText: 'Indexer client in Radarr ready.',
	},
	{
		stage: IntegrationSetupProgressStage.Validation,
		selector: 'validation',
		error: 'Radarr validation failed',
		successText: 'Radarr can communicate back to Reaparr.',
	},
	{
		stage: IntegrationSetupProgressStage.Done,
		selector: 'done',
		error: '',
	},
] as const;

interface RadarrUrlScenario {
	invalidUrls: readonly string[];
	enteredValidUrl: string;
	persistedUrl: string;
	integrations: IntegrationSummary[];
	detail: RadarrIntegrationDTO;
	summary: IntegrationSummary;
	calls: Record<'create' | 'test' | 'setup', number>;
}

function createRadarrUrlScenario(): RadarrUrlScenario {
	const persistedUrl = 'https://radarr.example.test:7979/radarr';
	return {
		invalidUrls: ['ftp://radarr.example.test:7979/radarr', 'not-a-valid-url'],
		enteredValidUrl: '  https://radarr.example.test:7979/radarr/  ',
		persistedUrl,
		integrations: [],
		detail: {
			...createRequest,
			id: createdIntegrationId,
			url: persistedUrl,
			provisioningState: IntegrationProvisioningState.Unconfigured,
			lastConnectionTestStatus: TestConnectionStatus.Unknown,
		},
		summary: {
			id: createdIntegrationId,
			type: IntegrationType.Radarr,
			name: createRequest.name,
			baseUrl: persistedUrl,
			category: createRequest.category,
			provisioningState: IntegrationProvisioningState.Unconfigured,
			lastConnectionTestStatus: TestConnectionStatus.Unknown,
			downloadFolderId: createRequest.downloadFolderId,
			externalDownloadClientId: null,
			externalIndexerId: null,
		},
		calls: { create: 0, test: 0, setup: 0 },
	};
}

function installRadarrUrlApis(scenario: RadarrUrlScenario): void {
	cy.basePageSetup({ plexAccountCount: 0, plexServerCount: 0 });
	cy.intercept('GET', IntegrationPaths.getIntegrationsEndpoint(), (request) => {
		request.reply({ statusCode: 200, body: generateResultDTO(scenario.integrations) });
	}).as('getIntegrations');
	cy.intercept('GET', IntegrationPaths.getRadarrIntegrationEndpoint(createdIntegrationId), (request) => {
		request.reply({ statusCode: 200, body: generateResultDTO(scenario.detail) });
	}).as('getRadarr');
	cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToRadarrEndpoint() }, (request) => {
		scenario.calls.test++;
		request.reply({
			statusCode: 200,
			body: generateResultDTO({ result: TestConnectionStatus.Success, errorMessage: null, httpStatusCode: 200, testedAt }),
		});
	}).as('testRadarrConnection');
	cy.intercept('POST', IntegrationPaths.createRadarrIntegrationEndpoint(), (request) => {
		const body = request.body as CreateRadarrIntegrationRequest;
		const normalizedUrl = body.url.trim().replace(/\/+$/, '');
		scenario.calls.create++;
		scenario.detail = { ...scenario.detail, url: normalizedUrl };
		scenario.summary = { ...scenario.summary, baseUrl: normalizedUrl };
		scenario.integrations = [scenario.summary];
		request.reply({ statusCode: 200, body: generateResultDTO(scenario.detail) });
	}).as('createRadarr');
	cy.intercept('POST', IntegrationPaths.setupRadarrIntegrationEndpoint(createdIntegrationId), (request) => {
		scenario.calls.setup++;
		request.reply({ statusCode: 200, body: generateResultDTO(scenario.detail) });
	}).as('setupRadarr');
}

function openRadarrUrlCreateDialog(): void {
	cy.visit(route('/settings/integrations'));
	cy.getPageData();
	cy.wait('@getIntegrations');
	cy.getCy('add-integration').click();
	cy.getCy('integration-dialog').should('be.visible');
	cy.getCy('integration-type-radarr').click();
	cy.getCy('integration-name').type(createRequest.name);
	cy.getCy('integration-arr-key').type(createRequest.apiKey);
	cy.getCy('integration-category').clear().type(createRequest.category);
	cy.getCy('integration-download-folder').click();
	cy.get('.q-menu').contains('.q-item', '/Downloads').click();
}

function assertInvalidRadarrUrls(scenario: RadarrUrlScenario): void {
	for (const invalidUrl of scenario.invalidUrls) {
		cy.getCy('integration-base-url').clear().type(invalidUrl).blur();
		cy.getCy('integration-base-url').closest('.q-field').should('contain.text', 'URL must be an absolute http/https URL.');
		cy.getCy('integration-save').should('be.disabled');
		cy.getCy('integration-test').should('be.disabled');
		cy.getCy('integration-setup').should('be.disabled');
		cy.then(() => expect(scenario.calls).to.deep.equal({ create: 0, test: 0, setup: 0 }));
	}
}

function saveValidRadarrUrl(scenario: RadarrUrlScenario): void {
	cy.getCy('integration-base-url').clear().type(scenario.enteredValidUrl).blur();
	cy.getCy('integration-base-url').should('have.value', scenario.enteredValidUrl);
	cy.getCy('integration-base-url').closest('.q-field').should('not.contain.text', 'URL must be an absolute http/https URL.');
	cy.getCy('integration-save').should('not.be.disabled');
	cy.getCy('integration-test').should('not.be.disabled');
	cy.getCy('integration-setup').should('be.disabled');
	cy.getCy('integration-save').click();
	cy.wait('@createRadarr').then(({ request, response }) => {
		expect(request.method).to.equal('POST');
		expect(request.body).to.deep.equal({ ...createRequest, url: scenario.enteredValidUrl });
		expect(response?.statusCode).to.equal(200);
		expect(response?.body.value).to.deep.include(scenario.detail);
	});
	cy.wait('@getIntegrations');
	cy.then(() => expect(scenario.calls).to.deep.equal({ create: 1, test: 0, setup: 0 }));
}

function assertSavedRadarrUrl(scenario: RadarrUrlScenario): void {
	cy.getCy('integration-dialog').should('not.exist');
	cy.getCy('integration-card')
		.should('have.length', 1)
		.and('be.visible')
		.and('contain.text', createRequest.name)
		.and('contain.text', scenario.persistedUrl)
		.and('contain.text', IntegrationProvisioningState.Unconfigured);
	cy.getCy('integration-card').click();
	cy.wait('@getRadarr');
	cy.getCy('integration-base-url').should('have.value', scenario.persistedUrl);
	cy.getCy('integration-dialog').find('[data-cy="dialog-close-button"]').click();
}

describe('Radarr integrations', () => {
	it('rejects non-http and malformed Radarr URLs before creating, then saves a valid offline URL', () => {
		const scenario = createRadarrUrlScenario();
		installRadarrUrlApis(scenario);
		openRadarrUrlCreateDialog();
		assertInvalidRadarrUrls(scenario);
		saveValidRadarrUrl(scenario);
		assertSavedRadarrUrl(scenario);
	});

	it('checks and saves a new Radarr integration as explicit separate actions', () => {
		let integrations: IntegrationSummary[] = [];
		let saveCallCount = 0;
		const createdDetail: RadarrIntegrationDTO = {
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
			type: IntegrationType.Radarr,
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
		cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToRadarrEndpoint() }, {
			delay: 1000,
			statusCode: 200,
			body: generateResultDTO({
				result: TestConnectionStatus.Success,
				errorMessage: null,
				httpStatusCode: 200,
				testedAt,
			}),
		}).as('testRadarrConnection');
		cy.intercept('POST', IntegrationPaths.createRadarrIntegrationEndpoint(), (request) => {
			saveCallCount++;
			integrations = [createdSummary];
			request.reply({ delay: 1000, statusCode: 200, body: generateResultDTO(createdDetail) });
		}).as('createRadarr');
		cy.visit(route('/settings/integrations'));
		cy.wait('@getIntegrations');

		cy.getCy('add-integration').click();
		cy.getCy('integration-dialog').should('be.visible');
		cy.getCy('integration-type-radarr').click();

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
		cy.wait('@testRadarrConnection').then(({ request, response }) => {
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
			.and('contain.text', 'Add Radarr integration');
		cy.getCy('integration-delete').should('not.exist');
		cy.getCy('integration-setup')
			.should('not.be.disabled')
			.and('contain.text', 'Save & Setup');
		cy.getCy('integration-save').click();
		cy.getCy('integration-save').find('.q-spinner').should('exist');
		cy.getCy('integration-test').find('.q-spinner').should('not.exist');
		cy.wait('@createRadarr').then(({ request, response }) => {
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
		cy.getCy('integration-card').find('[alt="radarr"]').should('exist');
	});

	it('saves and sets up a new Radarr integration before closing both dialogs', () => {
		let integrations: IntegrationSummary[] = [];
		let createCallCount = 0;
		let setupCallCount = 0;
		const createdDetail: RadarrIntegrationDTO = {
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
			type: IntegrationType.Radarr,
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
		cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToRadarrEndpoint() }, {
			statusCode: 200,
			body: generateResultDTO({
				result: TestConnectionStatus.Success,
				errorMessage: null,
				httpStatusCode: 200,
				testedAt,
			}),
		}).as('testRadarrConnection');
		cy.intercept('POST', IntegrationPaths.createRadarrIntegrationEndpoint(), (request) => {
			createCallCount++;
			integrations = [createdSummary];
			request.reply({ delay: 1000, statusCode: 200, body: generateResultDTO(createdDetail) });
		}).as('createRadarr');
		cy.intercept('POST', IntegrationPaths.setupRadarrIntegrationEndpoint(createdIntegrationId), (request) => {
			setupCallCount++;
			request.reply({ statusCode: 200, body: generateResultDTO(configuredDetail) });
		}).as('setupRadarr');
		cy.visit(route('/settings/integrations'));
		cy.wait('@getIntegrations');

		cy.getCy('add-integration').click();
		cy.getCy('integration-type-radarr').click();
		cy.getCy('integration-name').type(createRequest.name);
		cy.getCy('integration-base-url').type(createRequest.url);
		cy.getCy('integration-arr-key').type(createRequest.apiKey);
		cy.getCy('integration-category').clear().type(createRequest.category);
		cy.getCy('integration-download-folder').click();
		cy.get('.q-menu').contains('.q-item', '/Downloads').click();
		cy.getCy('integration-test').click();
		cy.wait('@testRadarrConnection');
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
		cy.wait('@createRadarr').then(({ request }) => {
			expect(request.body).to.deep.equal(createRequest);
		});
		cy.wait('@getIntegrations');
		cy.wait('@setupRadarr').then(({ request }) => {
			expect(request.url).to.contain(createdIntegrationId);
		});
		cy.then(() => {
			expect(createCallCount).to.equal(1);
			expect(setupCallCount).to.equal(1);
		});
		cy.getCy('integration-setup-dialog').should('be.visible');
		cy.getCy('integration-dialog')
			.should('exist')
			.and('contain.text', 'Add Radarr integration');
		cy.getCy('integration-setup-dialog').find('[data-cy="dialog-close-button"]').click();
		cy.getCy('integration-setup-dialog').should('not.exist');
		cy.getCy('integration-dialog').should('not.exist');
	});

	it('edits and sets up an existing unconfigured Radarr integration', () => {
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
		const configuredDetail: RadarrIntegrationDTO = {
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
		cy.intercept('GET', IntegrationPaths.getRadarrIntegrationEndpoint(integrationId), {
			statusCode: 200,
			body: generateResultDTO(currentDetail),
		}).as('getRadarr');
		cy.intercept({ method: 'GET', pathname: IntegrationPaths.testConnectionToRadarrEndpoint() }, (request) => {
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
		}).as('testRadarrConnection');
		cy.intercept('PUT', IntegrationPaths.updateRadarrIntegrationEndpoint(integrationId), (request) => {
			updateCallCount++;
			currentSummary = { ...currentSummary, name: request.body.name, baseUrl: request.body.url, category: request.body.category, downloadFolderId: request.body.downloadFolderId };
			currentDetail = { ...currentDetail, name: request.body.name, url: request.body.url, apiKey: request.body.apiKey, category: request.body.category, downloadFolderId: request.body.downloadFolderId };
			request.reply({ statusCode: 200, body: generateResultDTO(currentDetail) });
		}).as('updateRadarr');
		cy.intercept('POST', IntegrationPaths.setupRadarrIntegrationEndpoint(integrationId), (request) => {
			setupCallCount++;
			currentSummary = configuredSummary;
			currentDetail = configuredDetail;
			request.reply({ statusCode: 200, body: generateResultDTO(configuredDetail) });
		}).as('setupRadarr');
		cy.intercept('DELETE', IntegrationPaths.deleteRadarrIntegrationEndpoint(integrationId, { force: true }), (request) => {
			isDeleted = true;
			request.reply({ delay: 1000, statusCode: 204 });
		}).as('deleteRadarr');
		cy.visit(route('/settings/integrations'));
		cy.wait('@getIntegrations');

		cy.getCy('integration-card').click();
		cy.wait('@getRadarr');
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
		cy.wait('@testRadarrConnection').then(({ request, response }) => {
			expect(request.query).to.deep.include({ apiKey: integrationApiKey, url: updatedUrl, integrationId });
			expect(response?.statusCode).to.equal(200);
		});
		cy.getCy('integration-dialog').should('contain.text', 'Connection failed').and('contain.text', `URL attempted: ${updatedUrl}`);
		cy.get('[data-cy="q-card-dialog-cy"]').should('not.exist');
		cy.getCy('integration-test').click();
		cy.wait('@testRadarrConnection').then(({ request, response }) => {
			expect(request.query).to.deep.include({ apiKey: integrationApiKey, url: updatedUrl, integrationId });
			expect(response?.statusCode).to.equal(200);
		});
		cy.getCy('integration-dialog').should('contain.text', 'Connection successful');
		cy.getCy('integration-name').invoke('val', updatedName).trigger('input').trigger('change');
		cy.getCy('integration-name').should('have.value', updatedName);
		cy.getCy('integration-setup').should('contain.text', 'Save & Setup');
		cy.getCy('integration-save').click();
		cy.wait('@updateRadarr').then(({ request, response }) => {
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
		cy.wait('@getRadarr');
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
			cy.getCy(`integration-setup-step-${selector}`)
				.should('have.attr', 'data-status', 'error')
				.and('contain.text', error)
				.find('.q-stepper__tab')
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

		cy.wait('@setupRadarr').then(({ request, response }) => {
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
		cy.wait('@setupRadarr');
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
		cy.wait('@deleteRadarr').then(({ request, response }) => {
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
