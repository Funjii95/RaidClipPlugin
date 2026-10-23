import type {InventoryItem} from './types';

export const isCollectionItem = (item: InventoryItem): boolean =>
  item.collector != null && !item.slot;
