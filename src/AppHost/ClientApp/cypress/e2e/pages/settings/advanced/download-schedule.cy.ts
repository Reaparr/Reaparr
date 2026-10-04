import { route, headers } from '@fixtures';
import { SettingsPaths } from '@api/api-paths';
import { generateResultDTO, generateFailedResultDTO } from '@mock';
import type { SettingsModelDTO } from '@dto';
import { cloneDeep } from 'lodash-es';

describe('Half-hour download schedule', () => {
	beforeEach(() => {
		cy.basePageSetup({ plexAccountCount: 0, plexServerCount: 1 });
		cy.visit(route('/settings/advanced'));
	});

	it('Should preserve policy and manual caps through saves, failures, reloads, and timezone changes', () => {
		cy.getPageData().then((data) => {
			let committed = cloneDeep(data.settings);
			const originalServers = cloneDeep(committed.serverSettings);
			let rejectNextSave = false;
			cy.intercept('GET', SettingsPaths.getUserSettingsEndpoint(), (request) => {
				request.reply({ statusCode: 200, body: generateResultDTO(committed), ...headers });
			});
			cy.intercept('PUT', SettingsPaths.updateUserSettingsEndpoint(), (request) => {
				if (rejectNextSave) {
					rejectNextSave = false;
					request.reply({ statusCode: 503, body: generateFailedResultDTO(), ...headers });
					return;
				}
				committed = cloneDeep(request.body as SettingsModelDTO);
				request.reply({ statusCode: 200, body: generateResultDTO(committed), ...headers });
			}).as('saveSchedule');

			cy.reload();
			cy.get('[role="slider"] [tabindex="0"]').focus().type('{rightarrow}');
			cy.wait('@saveSchedule').its('request.body.downloadManagerSettings.downloadSegments').should('equal', 5);
			cy.getCy('schedule-cell-0-19').should('have.attr', 'aria-label').and('include', 'Unlimited');
			cy.getCy('schedule-weekday-work-hours').click();
			cy.getCy('schedule-limit-mode').click();
			cy.getCy('schedule-limit-input').find('input').clear().type('0');
			cy.getCy('schedule-apply').should('be.disabled');
			cy.getCy('schedule-limit-input').find('input').clear().type('4000').blur();
			cy.get('@saveSchedule.all').should('have.length', 1);
			cy.getCy('schedule-apply').should('not.be.disabled').click();
			cy.wait('@saveSchedule').then((interception) => {
				const saved = interception.request.body as SettingsModelDTO;
				const expected = Object.fromEntries(
					['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((day) => [day, { '09:00': 4000, '18:00': null }]));
				expect(interception.response?.statusCode).to.equal(200);
				expect(saved.downloadManagerSettings.downloadSchedule.days).to.deep.equal(expected);
				expect(saved.serverSettings).to.deep.equal(originalServers);
			});
			cy.getCy('schedule-enable').should('not.have.attr', 'aria-disabled', 'true').click();
			cy.wait('@saveSchedule').its('request.body.downloadManagerSettings.downloadSchedule.enabled').should('equal', true);

			cy.getCy('schedule-cell-0-19').click();
			cy.getCy('schedule-from').should('contain.text', '09:30');
			cy.getCy('schedule-limit-input').find('input').clear().type('6000').blur();
			cy.then(() => {
				rejectNextSave = true;
			});
			cy.getCy('schedule-apply').click();
			cy.wait('@saveSchedule').its('response.statusCode').should('equal', 503);
			cy.getCy('schedule-save-error').should('be.visible').and('contain.text', 'Save failed.');
			cy.getCy('schedule-apply').should('not.be.disabled');
			cy.getCy('schedule-cell-0-19').should('have.attr', 'aria-label').and('include', '6,000 kB/s');
			cy.get('[role="slider"] [tabindex="0"]').focus().type('{rightarrow}');
			cy.wait('@saveSchedule').then((interception) => {
				expect(interception.request.body.downloadManagerSettings.downloadSegments).to.equal(6);
				expect(interception.request.body.downloadManagerSettings.downloadSchedule.days.Monday)
					.to.deep.equal({ '09:00': 4000, '18:00': null });
			});
			cy.getCy('schedule-save-error').should('be.visible');
			cy.then(() => {
				expect(committed.downloadManagerSettings.downloadSchedule.days.Monday).to.deep.equal({ '09:00': 4000, '18:00': null });
			});
			cy.getCy('schedule-limit-input').find('input').should(($input) => {
				expect(String($input.val()).replace(/\D/g, '')).to.equal('6000');
			});
			cy.getCy('schedule-apply').click();
			cy.wait('@saveSchedule').then((interception) => {
				const recovered = interception.request.body as SettingsModelDTO;
				expect(interception.response?.statusCode).to.equal(200);
				expect(recovered.downloadManagerSettings.downloadSchedule.days.Monday).to.deep.equal({
					'09:00': 4000, '09:30': 6000, '10:00': 4000, '18:00': null,
				});
				expect(recovered.serverSettings).to.deep.equal(originalServers);
			});
			cy.getCy('schedule-save-error').should('not.exist');
			cy.reload();
			cy.getCy('schedule-enable').should('have.attr', 'aria-checked', 'true');
			cy.getCy('schedule-cell-0-19').should('have.attr', 'aria-label').and('include', '6,000 kB/s');
			cy.getCy('schedule-cell-0-20').should('have.attr', 'aria-label').and('include', '4,000 kB/s');
			cy.getCy('schedule-cell-0-36').should('have.attr', 'aria-label').and('include', 'Unlimited');
			cy.visit(route('/settings/ui'));
			cy.getCy('time-zone').should('have.attr', 'aria-label', 'Time Zone');
			cy.getCy('time-zone').clear().type('Africa/Algiers');
			cy.contains('[role="option"]', '(UTC+01:00) Africa/Algiers').then(($option) => {
				const zone = 'Africa/Algiers';
				const originalPolicy = cloneDeep(committed.downloadManagerSettings.downloadSchedule);
				cy.wrap($option).click();
				cy.wait('@saveSchedule').then((interception) => {
					expect(interception.request.body.dateTimeSettings.timeZone).to.equal(zone);
					expect(interception.request.body.downloadManagerSettings.downloadSchedule).to.deep.equal(originalPolicy);
					expect(interception.request.body.serverSettings).to.deep.equal(originalServers);
				});
				cy.reload();
				cy.getCy('time-zone').invoke('val').should('include', zone);
				cy.visit(route('/settings/advanced'));
				cy.getCy('download-schedule-section').should('contain.text', zone);
				cy.getCy('schedule-cell-0-19').should('have.attr', 'aria-label').and('include', '6,000 kB/s');
			});
		});
	});
});
