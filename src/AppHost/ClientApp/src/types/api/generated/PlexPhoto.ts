/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

import type { RequestParams } from "./http-client";

import type {
  PhotoAlbumPageDTO,
  PhotoAssetInspectionDTO,
  PhotoAssetPageDTO,
  PhotoFolderChildrenDTO,
  PhotoFolderPageDTO,
  PhotoImageRepresentation,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class PlexPhoto {
  /**
   * No description
   * * @tags Plexphoto
   * @name GetPhotoAlbumsEndpoint
   * @request GET:/api/PlexPhoto/albums
   * @secure
   */
  getPhotoAlbumsEndpoint = (
    query?: {
      /** @format int32 */
      page?: number | null;
      /** @format int32 */
      plexLibraryId?: number | null;
      /** @format int32 */
      size?: number | null;
      sort?: string | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PhotoAlbumPageDTO>({
      url: `/api/PlexPhoto/albums`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PhotoAlbumPageDTO>);

  /**
   * No description
   * * @tags Plexphoto
   * @name GetPhotoAssetsEndpoint
   * @request GET:/api/PlexPhoto/assets
   * @secure
   */
  getPhotoAssetsEndpoint = (
    query?: {
      /** @format int32 */
      page?: number | null;
      /** @format int32 */
      plexLibraryId?: number | null;
      /** @format int32 */
      size?: number | null;
      sort?: string | null;
      unmappedOnly?: boolean | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PhotoAssetPageDTO>({
      url: `/api/PlexPhoto/assets`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PhotoAssetPageDTO>);

  /**
   * No description
   * * @tags Plexphoto
   * @name SearchPhotoAssetsEndpoint
   * @request GET:/api/PlexPhoto/assets/search
   * @secure
   */
  searchPhotoAssetsEndpoint = (
    query?: {
      /** @format int32 */
      page?: number | null;
      q?: string | null;
      /** @format int32 */
      size?: number | null;
      sort?: string | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PhotoAssetPageDTO>({
      url: `/api/PlexPhoto/assets/search`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PhotoAssetPageDTO>);

  /**
   * No description
   * * @tags Plexphoto
   * @name GetPhotoAlbumMembersEndpoint
   * @request GET:/api/PlexPhoto/libraries/{plexLibraryId}/albums/{albumId}/assets
   * @secure
   */
  getPhotoAlbumMembersEndpoint = (
    plexLibraryId: number,
    albumId: number,
    query?: {
      /** @format int32 */
      page?: number | null;
      /** @format int32 */
      size?: number | null;
      sort?: string | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PhotoAssetPageDTO>({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/albums/${albumId}/assets`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PhotoAssetPageDTO>);

  /**
   * No description
   * * @tags Plexphoto
   * @name GetPhotoAssetInspectionEndpoint
   * @request GET:/api/PlexPhoto/libraries/{plexLibraryId}/assets/{assetId}
   * @secure
   */
  getPhotoAssetInspectionEndpoint = (
    plexLibraryId: number,
    assetId: number,
    query?: {
      /** @format int32 */
      membershipPage?: number | null;
      /** @format int32 */
      membershipSize?: number | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PhotoAssetInspectionDTO>({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/assets/${assetId}`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PhotoAssetInspectionDTO>);

  /**
   * @description Streams the original photo part or a Plex-side browser-compatible conversion.
   * * @tags Plexphoto
   * @name GetPhotoAssetImageEndpoint
   * @summary Deliver a photo image
   * @request GET:/api/PlexPhoto/libraries/{plexLibraryId}/assets/{assetId}/image
   * @secure
   */
  getPhotoAssetImageEndpoint = (
    plexLibraryId: number,
    assetId: number,
    query: {
      /** @format int32 */
      height?: number | null;
      /** @example "original" */
      representation: PhotoImageRepresentation;
      /** @format int32 */
      width?: number | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<Blob>({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/assets/${assetId}/image`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "blob",
      ...params,
    }).pipe(apiCheckPipe<Blob>);

  /**
   * No description
   * * @tags Plexphoto
   * @name GetPhotoFolderChildrenEndpoint
   * @request GET:/api/PlexPhoto/libraries/{plexLibraryId}/folders/{folderId}/children
   * @secure
   */
  getPhotoFolderChildrenEndpoint = (
    plexLibraryId: number,
    folderId: number,
    query?: {
      /** @format int32 */
      assetPage?: number | null;
      /** @format int32 */
      assetSize?: number | null;
      /** @format int32 */
      folderPage?: number | null;
      /** @format int32 */
      folderSize?: number | null;
      sort?: string | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PhotoFolderChildrenDTO>({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/folders/${folderId}/children`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PhotoFolderChildrenDTO>);

  /**
   * No description
   * * @tags Plexphoto
   * @name GetPhotoSourceRootsEndpoint
   * @request GET:/api/PlexPhoto/libraries/{plexLibraryId}/roots
   * @secure
   */
  getPhotoSourceRootsEndpoint = (
    plexLibraryId: number,
    query?: {
      /** @format int32 */
      page?: number | null;
      /** @format int32 */
      size?: number | null;
      sort?: string | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PhotoFolderPageDTO>({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/roots`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PhotoFolderPageDTO>);
}

export class PlexPhotoPaths {
  static getPhotoAlbumsEndpoint = (query?: {
    /** @format int32 */
    page?: number | null;
    /** @format int32 */
    plexLibraryId?: number | null;
    /** @format int32 */
    size?: number | null;
    sort?: string | null;
  }) => queryString.stringifyUrl({ url: `/api/PlexPhoto/albums`, query });

  static getPhotoAssetsEndpoint = (query?: {
    /** @format int32 */
    page?: number | null;
    /** @format int32 */
    plexLibraryId?: number | null;
    /** @format int32 */
    size?: number | null;
    sort?: string | null;
    unmappedOnly?: boolean | null;
  }) => queryString.stringifyUrl({ url: `/api/PlexPhoto/assets`, query });

  static searchPhotoAssetsEndpoint = (query?: {
    /** @format int32 */
    page?: number | null;
    q?: string | null;
    /** @format int32 */
    size?: number | null;
    sort?: string | null;
  }) =>
    queryString.stringifyUrl({ url: `/api/PlexPhoto/assets/search`, query });

  static getPhotoAlbumMembersEndpoint = (
    plexLibraryId: number,
    albumId: number,
    query?: {
      /** @format int32 */
      page?: number | null;
      /** @format int32 */
      size?: number | null;
      sort?: string | null;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/albums/${albumId}/assets`,
      query,
    });

  static getPhotoAssetInspectionEndpoint = (
    plexLibraryId: number,
    assetId: number,
    query?: {
      /** @format int32 */
      membershipPage?: number | null;
      /** @format int32 */
      membershipSize?: number | null;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/assets/${assetId}`,
      query,
    });

  static getPhotoAssetImageEndpoint = (
    plexLibraryId: number,
    assetId: number,
    query: {
      /** @format int32 */
      height?: number | null;
      /** @example "original" */
      representation: PhotoImageRepresentation;
      /** @format int32 */
      width?: number | null;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/assets/${assetId}/image`,
      query,
    });

  static getPhotoFolderChildrenEndpoint = (
    plexLibraryId: number,
    folderId: number,
    query?: {
      /** @format int32 */
      assetPage?: number | null;
      /** @format int32 */
      assetSize?: number | null;
      /** @format int32 */
      folderPage?: number | null;
      /** @format int32 */
      folderSize?: number | null;
      sort?: string | null;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/folders/${folderId}/children`,
      query,
    });

  static getPhotoSourceRootsEndpoint = (
    plexLibraryId: number,
    query?: {
      /** @format int32 */
      page?: number | null;
      /** @format int32 */
      size?: number | null;
      sort?: string | null;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexPhoto/libraries/${plexLibraryId}/roots`,
      query,
    });
}
