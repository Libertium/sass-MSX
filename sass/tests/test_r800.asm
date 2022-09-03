.org	0x8500

ld a,(0x8600)
ld c,a
ld a,(0x8601)
.db 0xED, 0xC9	; MULUB A,C
; In MULUB instructions the result is always stored to HL and in MULUW instructions the result is always stored to DE:HL
ld a,d
ld (0x8602),a
ld a,e
ld (0x8603),a
ld a,h
ld (0x8604),a
ld a,l
ld (0x8605),a
ret


