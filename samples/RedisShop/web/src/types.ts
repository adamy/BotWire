/** Mirrors the RedisShop.Api Product record. */
export interface Product {
  id: string;
  name: string;
  category: string;
  price: number;
  description: string;
  inStock: boolean;
}
