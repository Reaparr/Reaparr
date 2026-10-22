import { headers, route } from '@fixtures';
import { generateFailedResultDTO, generateResultDTO } from '@mock';
import { FolderPathPaths } from '@api/api-paths';
import { FileSystemEntityType, FolderType, PlexMediaType, type FileSystemDTO, type FolderPathDTO } from '@dto';
import { kebabCase } from 'lodash-es';

const newFamilies = [
	{
		folderType: FolderType.MusicFolder,
		mediaType: PlexMediaType.MusicArtist,
		directory: 'Music',
		defaultName: 'Music Folder Path',
	},
	{
		folderType: FolderType.PhotosFolder,
		mediaType: PlexMediaType.PhotoAlbum,
		directory: 'Photos',
		defaultName: 'Photos Folder Path',
	},
	{
		folderType: FolderType.OtherVideosFolder,
		mediaType: PlexMediaType.OtherVideos,
		directory: 'Other',
		defaultName: 'Other Videos Folder Path',
	},
] as const;

const allFamilies = [
	{ folderType: FolderType.DownloadFolder },
	{ folderType: FolderType.MovieFolder },
	{ folderType: FolderType.TvShowFolder },
	...newFamilies,
] as const;

function setupPathsPage() {
	cy.intercept('**/api/**', (request) => {
		throw new Error(`Unmocked API request: ${request.method} ${request.url}`);
	});

	cy.basePageSetup({
		plexAccountCount: 0,
		plexServerCount: 0,
		invalidDefaultFolderPaths: true,
	}).then(({ folderPaths }) => {
		let persistedFolderPaths = folderPaths.map((folderPath) => ({ ...folderPath }));
		let nextId = 11;

		cy.intercept('GET', '**/api/Integration', {
			statusCode: 200,
			body: generateResultDTO([]),
			...headers,
		});
		cy.intercept('GET', FolderPathPaths.getAllFolderPathsEndpoint(), (request) => {
			request.reply({ statusCode: 200, body: generateResultDTO(persistedFolderPaths), ...headers });
		}).as('getFolderPaths');
		cy.intercept('POST', FolderPathPaths.createFolderPathEndpoint(), (request) => {
			const body = request.body as FolderPathDTO;
			const created: FolderPathDTO = { ...body, id: nextId++, isValid: true };
			persistedFolderPaths.push(created);
			request.reply({ statusCode: 200, body: generateResultDTO(created), ...headers });
		}).as('createFolderPath');
		cy.intercept('PUT', FolderPathPaths.updateFolderPathEndpoint(), (request) => {
			const body = request.body as FolderPathDTO;
			const index = persistedFolderPaths.findIndex((folderPath) => folderPath.id === body.id);
			if (index === -1) {
				throw new Error(`Cannot update unknown folder path ${body.id}`);
			}
			persistedFolderPaths[index] = { ...body, isValid: true };
			request.reply({ statusCode: 200, body: generateResultDTO(persistedFolderPaths[index]), ...headers });
		}).as('saveFolderPath');
		cy.intercept('DELETE', '**/api/FolderPath/*', (request) => {
			const id = Number(request.url.split('/').at(-1));
			persistedFolderPaths = persistedFolderPaths.filter((folderPath) => folderPath.id !== id);
			request.reply({ statusCode: 200, body: generateResultDTO(true), ...headers });
		}).as('deleteFolderPath');
	});

	cy.visit(route('/settings/paths'));
}

function directoryLookupResult(directory: string, writable: boolean): FileSystemDTO {
	return {
		current: directory
			? {
					name: directory.split('/').at(-1) || '/',
					path: directory,
					type: FileSystemEntityType.Folder,
					extension: '',
					size: 0,
					lastModified: '',
					hasReadPermission: true,
					hasWritePermission: writable,
				}
			: null,
		directories: [],
		files: [],
		parent: '/',
	};
}

function installPendingDirectoryLookup() {
	let pending = false;
	const releases: Array<() => void> = [];
	cy.intercept('GET', '**/api/FolderPath/directory*', (request) => {
		const directory = request.query.path;
		if (typeof directory !== 'string') throw new Error('Expected a directory lookup path');
		if (directory === '/failed') {
			request.alias = 'failedDirectory';
			request.reply({
				statusCode: 500,
				body: generateFailedResultDTO({
					statusCode: 500,
					errors: [{ message: 'Directory lookup unavailable', reasons: [], metadata: {} }],
				}),
				...headers,
			});
			return;
		}
		if (directory === '/pending') {
			request.on('before:response', () => {
				pending = true;
				return new Promise<void>((resolve) => {
					releases.push(resolve);
				});
			});
		} else if (directory === '/read-only') {
			request.alias = 'readOnlyDirectory';
		} else if (directory === '/verified') {
			request.alias = 'verifiedDirectory';
		}
		request.reply({
			statusCode: 200,
			body: generateResultDTO(directoryLookupResult(directory, directory !== '/read-only')),
			...headers,
		});
	});
	return {
		get pending() { return pending; },
		release: () => releases.splice(0).forEach((resolve) => resolve()),
	};
}

function chooseRootDirectory(directory: string) {
	cy.getCy('directory-browser-rows').within(() => cy.contains('tr', directory).click());
	cy.getCy('directory-browser-confirm-button').click();
}

function assertTouchControl(selector: string) {
	cy.getCy(selector).should(($control) => {
		const rect = $control[0]!.getBoundingClientRect();
		expect(rect.width, `${selector} touch width`).to.be.gte(44);
		expect(rect.height, `${selector} touch height`).to.be.gte(44);
	});
}

describe('Change Folder Paths', () => {
	let releaseDirectoryLookup: (() => void) | undefined;
	afterEach(() => {
		releaseDirectoryLookup?.();
		releaseDirectoryLookup = undefined;
	});

	beforeEach(() => {
		setupPathsPage();
	});

	it('sets the correct destination for each default folder with the directory browser', () => {
		cy.getPageData().then(() => cy.correctDefaultFolderPaths());
	});

	it('keeps lookup progress nonblocking and saves only the latest resolved writable directory', () => {
		cy.getPageData().then(() => {
			const lookup = installPendingDirectoryLookup();
			releaseDirectoryLookup = lookup.release;
			const type = kebabCase(FolderType.MovieFolder);
			cy.getCy(`folder-path-tab-${type}`).click();
			cy.getCy(`default-${type}-edit-button`).click();
			cy.getCy('directory-browser-confirm-button').should('be.visible').and('not.be.disabled');
			cy.getCy('directory-browser-path-field').find('.q-spinner').should('not.exist');

			cy.get('.q-dialog .dialog-container-top-row input').clear().type('/pending');
			cy.wrap(null).should(() => expect(lookup.pending).to.equal(true));
			cy.getCy('directory-browser-path-field').find('.q-spinner').should('be.visible');
			cy.getCy('directory-browser-path-field').find('input').should('have.attr', 'aria-busy', 'true');
			cy.getCy('directory-browser-confirm-button').should('be.disabled').click({ force: true });
			cy.getCy('directory-browser-row-return').should('not.exist');
			cy.get('.q-dialog .dialog-container-top-row input').should('not.be.disabled').and('not.have.attr', 'readonly');
			cy.get('@saveFolderPath.all').should('have.length', 0);

			cy.getCy('directory-browser-path-field').find('input').clear().type('/failed');
			cy.wait('@failedDirectory').its('response.statusCode').should('equal', 500);
			cy.getCy('directory-browser-path-field').find('.q-spinner').should('not.exist');
			cy.getCy('alert-dialog').should('be.visible').and('contain.text', 'Directory lookup unavailable');
			cy.getCy('close-alert-dialog').click();
			cy.getCy('directory-browser-confirm-button').should('be.disabled');

			cy.get('.q-dialog .dialog-container-top-row input').clear().type('/read-only');
			cy.wait('@readOnlyDirectory').its('response.statusCode').should('equal', 200);
			cy.getCy('directory-browser-path-field').find('.q-spinner').should('not.exist');
			cy.getCy('directory-browser-path-field').find('input').should('have.attr', 'aria-busy', 'false');
			cy.getCy('directory-browser-row-return').should('be.visible');
			cy.getCy('directory-browser-confirm-button').should('be.disabled');
			cy.then(() => lookup.release());
			cy.get('.q-dialog .dialog-container-top-row input').should('have.value', '/read-only');
			cy.getCy('directory-browser-path-field').find('.q-spinner').should('not.exist');
			cy.getCy('directory-browser-confirm-button').should('be.disabled').click({ force: true });
			cy.get('@saveFolderPath.all').should('have.length', 0);

			cy.get('.q-dialog .dialog-container-top-row input').clear().type('/verified');
			cy.wait('@verifiedDirectory').its('response.statusCode').should('equal', 200);
			cy.getCy('directory-browser-confirm-button').should('not.be.disabled').click();
			cy.wait('@saveFolderPath').its('request.body').should('deep.include', {
				directory: '/verified', folderType: FolderType.MovieFolder, isDefault: true,
			});
			cy.getCy(`default-${type}-input`).should('have.value', '/verified');
			cy.reload();
			cy.wait('@getFolderPaths');
			cy.getCy(`folder-path-tab-${type}`).click();
			cy.getCy(`default-${type}-input`).should('have.value', '/verified');
		});
	});

	it('creates each new media destination and manages a custom music destination through reloads', () => {
		cy.getPageData();

		newFamilies.forEach((family, index) => {
			const type = kebabCase(family.folderType);
			cy.getCy(`folder-path-tab-${type}`).click();
			cy.getCy(`default-${type}-row`).should('be.visible');
			cy.getCy(`default-${type}-delete-button`).should('not.exist');
			cy.getCy(`default-${type}-edit-button`).click();
			cy.getCy('directory-browser-row-return').click();
			chooseRootDirectory(family.directory);
			cy.wait('@saveFolderPath').its('request.body').should('deep.include', {
				id: 4 + index,
				directory: `/${family.directory}`,
				folderType: family.folderType,
				mediaType: family.mediaType,
				isDefault: true,
			});
			cy.getCy(`default-${type}-input`).should('have.value', `/${family.directory}`);
			cy.getCy(`${type}-add-button`).click();
			chooseRootDirectory(family.directory);
			cy.wait('@createFolderPath').then(({ request, response }) => {
				expect(request.body).to.deep.include({
					id: 0,
					directory: `/${family.directory}`,
					displayName: family.defaultName,
					folderType: family.folderType,
					mediaType: family.mediaType,
					isDefault: false,
				});
				expect(response?.body.value.id).to.equal(11 + index);
			});
			cy.get('@createFolderPath.all').should('have.length', index + 1);
			cy.getCy(`custom-${type}-row`).should('be.visible');
			cy.getCy(`custom-${type}-input`).should('have.value', `/${family.directory}`);

			for (const otherFamily of newFamilies.filter((candidate) => candidate !== family)) {
				cy.getCy(`folder-path-tab-${kebabCase(otherFamily.folderType)}`).click();
				cy.getCy(`custom-${type}-row`).should('not.exist');
			}
		});

		cy.reload();
		cy.wait('@getFolderPaths');
		for (const family of newFamilies) {
			const type = kebabCase(family.folderType);
			cy.getCy(`folder-path-tab-${type}`).click();
			cy.getCy(`default-${type}-input`).should('have.value', `/${family.directory}`);
			cy.getCy(`custom-${type}-input`).should('have.value', `/${family.directory}`);
		}

		const musicType = kebabCase(FolderType.MusicFolder);
		cy.getCy(`folder-path-tab-${musicType}`).click();
		cy.getCy(`custom-${musicType}-edit-button`).click();
		cy.getCy('directory-browser-row-return').click();
		chooseRootDirectory('Photos');
		cy.wait('@saveFolderPath').its('request.body').should('deep.include', {
			id: 11,
			directory: '/Photos',
			isDefault: false,
		});
		cy.getCy(`custom-${musicType}-input`).should('have.value', '/Photos');

		cy.getCy(`custom-${musicType}-row`).parents('.folder-path-row').find('.editable-text-item').click();
		cy.get('.q-popup-edit input').clear().type('Listening Room{enter}');
		cy.wait('@saveFolderPath').its('request.body').should('deep.include', {
			id: 11,
			displayName: 'Listening Room',
			isDefault: false,
		});
		cy.getCy(`custom-${musicType}-row`).parents('.folder-path-row').should('contain.text', 'Listening Room');

		cy.getCy(`custom-${musicType}-delete-button`).click();
		cy.getCy('confirmation-dialog-cancel-button').click();
		cy.get('@deleteFolderPath.all').should('have.length', 0);
		cy.getCy(`custom-${musicType}-row`).should('be.visible');
		cy.getCy(`custom-${musicType}-delete-button`).click();
		cy.getCy('confirmation-dialog-confirmation-button').click();
		cy.wait('@deleteFolderPath').its('request.url').should('match', /\/api\/FolderPath\/11$/);
		cy.getCy(`custom-${musicType}-row`).should('not.exist');

		cy.reload();
		cy.wait('@getFolderPaths');
		cy.getCy(`folder-path-tab-${musicType}`).click();
		cy.getCy(`default-${musicType}-input`).should('have.value', '/Music');
		cy.getCy(`custom-${musicType}-row`).should('not.exist');
		cy.getCy('folder-path-tab-photos-folder').click();
		cy.getCy('custom-photos-folder-input').should('have.value', '/Photos');
		cy.get<{ request: { body: FolderPathDTO } }[]>('@saveFolderPath.all').then((calls) => {
			expect(calls.map(({ request }) => request.body.id)).to.deep.equal([4, 5, 6, 11, 11]);
		});
	});
	function createCustomMusicDestination() {
		cy.getCy('folder-path-tab-music-folder').click();
		cy.getCy('music-folder-add-button').click();
		chooseRootDirectory('Music');
		cy.wait('@createFolderPath');
	}

	function failedWrite(message: string) {
		return generateFailedResultDTO({
			statusCode: 500,
			errors: [{ message, reasons: [], metadata: {} }],
		});
	}

	const directoryWrites = [
		{
			operation: 'update',
			method: 'PUT',
			url: FolderPathPaths.updateFolderPathEndpoint(),
			openButton: 'default-music-folder-edit-button',
			navigateToRoot: true,
			directory: 'Photos',
			resultInput: 'default-music-folder-input',
			resultPath: '/Photos',
			successAlias: '@saveFolderPath',
		},
		{
			operation: 'create',
			method: 'POST',
			url: FolderPathPaths.createFolderPathEndpoint(),
			openButton: 'music-folder-add-button',
			navigateToRoot: false,
			directory: 'Music',
			resultInput: 'custom-music-folder-input',
			resultPath: '/Music',
			successAlias: '@createFolderPath',
		},
	];

	for (const scenario of directoryWrites) {
		it(`retains a failed ${scenario.operation} directory draft until its retry succeeds`, () => {
			cy.getPageData();
			const message = `${scenario.operation} persistence unavailable`;
			cy.getCy('folder-path-tab-music-folder').click();
			cy.getCy(scenario.openButton).click();
			if (scenario.navigateToRoot) {
				cy.getCy('directory-browser-row-return').click();
			}
			cy.intercept({ method: scenario.method, url: scenario.url, times: 1 }, {
				statusCode: 500,
				body: failedWrite(message),
				...headers,
			}).as('failedDirectoryWrite');
			chooseRootDirectory(scenario.directory);
			cy.wait('@failedDirectoryWrite').its('response.statusCode').should('eq', 500);
			cy.getCy('alert-dialog').should('be.visible').within(() => {
				cy.getCy('api-error').should('contain.text', message);
				cy.getCy('close-alert-dialog').click();
			});
			cy.getCy('alert-dialog').should('not.exist');
			cy.getCy('directory-browser-save-error').should('be.visible').and('contain.text', message);
			if (scenario.operation === 'create') {
				cy.getCy(scenario.resultInput).should('not.exist');
			} else {
				cy.getCy(scenario.resultInput).should('have.value', '/WRONG-PATH');
			}
			cy.getCy('directory-browser-confirm-button').should('not.be.disabled').click();
			cy.wait(scenario.successAlias);
			cy.getCy('directory-browser-confirm-button').should('not.exist');
			cy.getCy(scenario.resultInput).should('have.value', scenario.resultPath);
		});
	}

	it('keeps the persisted name after failure and applies a successful rename retry', () => {
		cy.getPageData();
		createCustomMusicDestination();
		const row = 'custom-music-folder-row';
		const message = 'Name persistence unavailable';
		cy.getCy(row).parents('.folder-path-row').find('.editable-text-item').click();
		cy.get('.q-popup-edit input').clear().type('Retry Music Name');
		cy.intercept({ method: 'PUT', url: FolderPathPaths.updateFolderPathEndpoint(), times: 1 }, {
			statusCode: 500,
			body: failedWrite(message),
			...headers,
		}).as('failedName');
		cy.get('.q-popup-edit input').type('{enter}');
		cy.wait('@failedName');
		cy.getCy('alert-dialog').should('be.visible').within(() => {
			cy.getCy('api-error').should('contain.text', message);
			cy.getCy('close-alert-dialog').click();
		});
		cy.getCy('alert-dialog').should('not.exist');
		cy.contains('[role="alert"]', message).should('be.visible');
		cy.getCy(row).parents('.folder-path-row').should('not.contain.text', 'Retry Music Name');
		cy.getCy(row).parents('.folder-path-row').find('.editable-text-item').click();
		cy.get('.q-popup-edit input').clear().type('Retry Music Name{enter}');
		cy.wait('@saveFolderPath');
		cy.getCy(row).parents('.folder-path-row').should('contain.text', 'Retry Music Name');
	});

	it('retains the selected row and failed delete confirmation until a retry succeeds', () => {
		cy.getPageData();
		createCustomMusicDestination();
		const message = 'Delete persistence unavailable';
		cy.getCy('custom-music-folder-delete-button').click();
		cy.intercept({ method: 'DELETE', url: '**/api/FolderPath/*', times: 1 }, {
			statusCode: 500,
			body: failedWrite(message),
			...headers,
		}).as('failedDelete');
		cy.getCy('confirmation-dialog-confirmation-button').click();
		cy.wait('@failedDelete');
		cy.getCy('alert-dialog').should('be.visible').within(() => {
			cy.getCy('api-error').should('contain.text', message);
			cy.getCy('close-alert-dialog').click();
		});
		cy.getCy('alert-dialog').should('not.exist');
		cy.getCy('confirmation-dialog').should('be.visible').and('contain.text', message);
		cy.getCy('custom-music-folder-input').should('have.value', '/Music');
		cy.getCy('confirmation-dialog-confirmation-button').click();
		cy.wait('@deleteFolderPath');
		cy.getCy('confirmation-dialog').should('not.exist');
		cy.getCy('custom-music-folder-row').should('not.exist');
	});

	it('keeps all destination tabs and controls reachable on a 320px phone', () => {
		cy.viewport(320, 568);
		cy.getPageData();

		cy.get('.folder-path-tabs .q-tab').should('have.length', 6).each(($tab) => {
			expect($tab.attr('aria-label'), 'accessible tab name').not.to.equal('');
			cy.wrap($tab).click().should('have.attr', 'aria-selected', 'true');
			cy.wrap($tab).find('.q-icon').should('be.visible');
		});
		cy.get('.folder-path-tab-label').should('not.be.visible');
		cy.document().should((document) => {
			expect(document.documentElement.scrollWidth, 'page content fits the viewport').to.be.lte(document.documentElement.clientWidth + 1);
		});

		for (const family of allFamilies) {
			const type = kebabCase(family.folderType);
			cy.getCy(`folder-path-tab-${type}`).click();
			cy.getCy(`default-${type}-row`).should('be.visible');
			cy.getCy(`default-${type}-delete-button`).should('not.exist');
			cy.getCy(`default-${type}-row`).parents('.folder-path-row').find('.editable-text-item').should('not.exist');
			assertTouchControl(`default-${type}-edit-button`);
			assertTouchControl(`${type}-add-button`);
		}
		cy.document().should((document) => {
			expect(document.documentElement.scrollWidth, 'page content fits after visiting every tab').to.be.lte(document.documentElement.clientWidth + 1);
		});
	});
});
