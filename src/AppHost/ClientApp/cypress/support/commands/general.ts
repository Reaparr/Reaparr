import type { IBasePageSetupResult } from '@fixtures';
import { basePageSetup, route } from '@fixtures';
import type { MockConfig } from '@mock';
import Log from 'consola';

Cypress.Commands.add('visitEmptyPage', () => cy.visit(route('/empty')).as('emptyPage'));

Cypress.Commands.add('getCy', (selector: string, options = {}) => cy.get(`[data-cy="${selector}"]`, options));

Cypress.Commands.add('basePageSetup', (config: Partial<MockConfig> = {}) => basePageSetup(config).as('pageData'));

Cypress.Commands.add('getPageData', () =>
	cy
		.get('[data-cy="page-load-completed"]', { timeout: 10000 }) // Wait for page load
		.get('@pageData')
		.then((x) => {
			const data = x as unknown as IBasePageSetupResult;
			Log.info('PageData generated:', data);
			cy.log('PageData generated:', data);
			return cy.wrap(data);
		}),
);

Cypress.Commands.add('awaitSettingsUpdate', () => cy.wait('@settingsUpdate'));
