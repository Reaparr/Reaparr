import { headers, route } from '@fixtures';
import type { SettingsModelDTO } from '@dto';
import { SettingsPaths } from '@api/api-paths';
import { generateResultDTO } from '@mock';

describe('Change UI settings', () => {
	beforeEach(() => {
		cy.basePageSetup({
			plexAccountCount: 0,
			plexServerCount: 0,
		});

		cy.visit(route('/settings/ui'));
	});

	it('Should change language to German when language selector is changed', () => {
		cy.getPageData().then(() => {
			// Change language
			cy.getCy('language-selector').click();
			cy.getCy('option-de-DE').click();

			// Change short date format
			cy.getCy('short-date-format').click();
			cy.getCy('option-yyyy-MM-dd').click();

			// Change long date format
			cy.getCy('long-date-format').click();
			cy.getCy('option-EEEE, MMMM dd, yyyy').click();

			// Change long date format
			cy.getCy('time-format').click();
			cy.getCy('option-pp').click();

			// Change relative dates
			cy.getCy('relative-date').click();

			// Change relative dates
			cy.getCy('ask-download-movie-confirmation').click();
			cy.getCy('ask-download-tvshow-confirmation').click();
			cy.getCy('ask-download-season-confirmation').click();
			cy.getCy('ask-download-episode-confirmation').click();

			cy.awaitSettingsUpdate().then((interception) => {
				const settings = interception.request.body as SettingsModelDTO;
				// Change language
				expect(settings.languageSettings.language).to.equal('de-DE');

				// Change short date format
				expect(settings.dateTimeSettings.shortDateFormat).to.equal('yyyy-MM-dd');

				// Change long date format
				expect(settings.dateTimeSettings.longDateFormat).to.equal('EEEE, MMMM dd, yyyy');

				// Change time format
				expect(settings.dateTimeSettings.timeFormat).to.equal('pp');

				// Change relative dates
				expect(settings.dateTimeSettings.showRelativeDates).to.equal(true);

				// Change confirmation settings
				expect(settings.confirmationSettings.askDownloadMovieConfirmation).to.equal(false);
				expect(settings.confirmationSettings.askDownloadTvShowConfirmation).to.equal(false);
				expect(settings.confirmationSettings.askDownloadSeasonConfirmation).to.equal(false);
				expect(settings.confirmationSettings.askDownloadEpisodeConfirmation).to.equal(false);
			});
		});
	});

	it('Should persist all six independent family confirmation switches across reload without changing Movie or TV', () => {
		cy.getPageData().then(({ settings }) => {
			cy.intercept('GET', SettingsPaths.getUserSettingsEndpoint(), (request) => {
				request.reply({ statusCode: 200, body: generateResultDTO(settings), ...headers });
			});
			const controls = [
				['music-artist', 'askDownloadMusicArtistConfirmation'],
				['music-album', 'askDownloadMusicAlbumConfirmation'],
				['music-track', 'askDownloadMusicTrackConfirmation'],
				['photo-album', 'askDownloadPhotoAlbumConfirmation'],
				['photo-image', 'askDownloadPhotoImageConfirmation'],
				['other-videos', 'askDownloadOtherVideosConfirmation'],
			] as const;
			for (const [control, field] of controls) {
				cy.getCy(`ask-download-${control}-confirmation`).scrollIntoView().should('have.attr', 'aria-checked', 'true').click();
				cy.awaitSettingsUpdate().its(`request.body.confirmationSettings.${field}`).should('eq', false);
			}
			cy.reload();
			cy.getPageData();
			for (const [control] of controls) cy.getCy(`ask-download-${control}-confirmation`).should('have.attr', 'aria-checked', 'false');
			for (const control of ['movie', 'tvshow', 'season', 'episode']) {
				cy.getCy(`ask-download-${control}-confirmation`).should('have.attr', 'aria-checked', 'true');
			}
		});
	});
});
