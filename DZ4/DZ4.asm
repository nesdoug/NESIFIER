; DZ4 v 5
; by Doug Fraker 2021
; NES compression system
; decompression code, asm for ca65
; writes directly to the PPU $2007

; block header, 1 byte format
; MMM C CCCC +1 = 1-32 count

; 00 M=0 literal, followed by the literal bytes
; 20 M=1 RLE (1 byte), followed by 1 byte
; 40 M=2 SEQUENTIAL (+1 each loop), followed by 1 byte
; 60 M=3 RLE (2 byte), followed by 2 bytes
; 80 M=4 RLE of 00, no following byte
; A0 M=5 RLE of FF, no following byte 
; C0 M=6 backwards reference MMM C CCCC, C +1 = 1-32 bytes copied, 
;        next byte = this address -256 + value
; E0 M=7 long 2 byte header 
;		 111 MMM CC  CCCC CCCC + 1 = 1-1024 count
; FF M=7 FF = exit

;2 byte headers
;e0 LIT
;e4 RLE
;e8 SEQ
;ec RLE 2 byte
;f0 RLE 00
;f4 RLE FF
;f8-fe unused.




;4 variables in the zeropage
;PTR_L, PTR_H, CNT_L, CNT_H

.segment "ZEROPAGE"
PTR_L: 	.res 1
PTR_H: 	.res 1
CNT_L: 	.res 1
CNT_H: 	.res 1




.segment "CODE"

;before this, set an address in the PPU
;screen should be off
;a = address of data L
;x = address of data H

dz4:
	sta PTR_L
	stx PTR_H
dz4_loop_y:
	ldy #0
dz4_loop:
	jsr @dz4_read_inc
	tax ;save
	jsr @get_cnt
	txa
@parse:	
	and #$e0
	beq @literal
	cmp #$20
	beq @rle
	cmp #$40
	beq @seq
	cmp #$60
	beq @rle2
	jmp @header2
	
	
	
@literal:
@loop1:
	jsr @dz4_read_inc ;get new value each loop
	sta $2007
	dec CNT_L
	bne @loop1
	dec CNT_H ;is off by 1
	bpl @loop1 ;but guaranteed to be < 128
	jmp dz4_loop
	
	
@rle:
	jsr @dz4_read_inc ;repeat same value each loop
@rle_common:
@loop2:	;don't change this label name, referenced far below
	sta $2007
	dec CNT_L
	bne @loop2
	dec CNT_H ;is off by 1
	bpl @loop2 ;but guaranteed to be < 128
	jmp dz4_loop


@seq:
	jsr @dz4_read_inc ;repeat same value each loop, add 1
	tay
@loop3:	
	sty $2007
	iny
	dec CNT_L
	bne @loop3
	dec CNT_H ; is off by 1
	bpl @loop3 ; but guaranteed to be < 128
	jmp dz4_loop_y ;also y=#0
	
	
;2 byte RLE	
@rle2:
@rle2_common:
	jsr @dz4_read_inc ;1st value
	tax
	jsr @dz4_read_inc ;2nd value
@loop4:	
	stx $2007
	sta $2007
	dec CNT_L
	bne @loop4
	dec CNT_H ; is off by 1
	bpl @loop4 ; but guaranteed to be < 128
	jmp dz4_loop
	
	
	
	
@header2:
	cmp #$80
	beq @rle00
	cmp #$a0
	beq @rleff
	cmp #$c0
	beq @block_copy
;#$e0-ff
	txa
	cmp #$ff
	bne @process_long ;e0
@return: ;$ff exit
	rts
	
	




@rle00:
	lda #0
	jmp @rle_common

@rleff:
	lda #$ff
	jmp @rle_common	


@block_copy:
;block copy, backwards MMM CCCCC, C = 2-32 bytes copied, 
;next byte = this address -256 + value (copy from data, not output stream)
	txa
	and #$1f
	sta CNT_L
	inc CNT_L ;off by 1
;	jsr @dz4_read_inc ;don't increment it yet...
	lda (PTR_L), y 
	tay
	dec PTR_H ;-256 + y
@loop6:
	lda (PTR_L), y
	sta $2007
	iny
	dec CNT_L
	bne @loop6
	inc PTR_H
	jsr @dz4_inc_no_read ;now increment it
	jmp dz4_loop_y ;also y=#0


;e0	
@process_long:
	txa
	and #3
	sta CNT_H
	jsr @dz4_read_inc
	sta CNT_L
	inc CNT_L
;CNT_L at zero works like 256, so we don't need to
;inc the CNT_H
	txa ;original header byte
	asl a
	asl a
	asl a
	jmp @parse ;does AND #$e0


	


;read 1 and increment the pointer
;y should always be zero here
@dz4_read_inc:
	lda (PTR_L), y
@dz4_inc_no_read:
	inc PTR_L
	bne @exit
	inc PTR_H
@exit:
	rts	
	

@get_cnt: ;short
;get the count
;header byte is in X
	txa
	and #$1f
	sta CNT_L
	inc CNT_L
	lda #0
	sta CNT_H
	rts
	
	

	
	

	

 
	