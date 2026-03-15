-- Adds email consent flags to `public.mitglied`
-- Default for both columns is `false`.

alter table public.mitglied
    add column if not exists email_info_einwilligung boolean not null default false;

alter table public.mitglied
    add column if not exists email_rechnung_einwilligung boolean not null default false;
